using UnityEngine;

namespace BBBNexus
{
    // 远程武器通用行为基类 负责装备瞄准开火IK后坐力等
    // 具体武器（AK47/Cannon 等）只需继承并指定各自的 SO 类型
    public abstract class RangedWeaponBehaviourBase : MonoBehaviour, IHoldableItem, IPoolable
    {
        [Header("--- 表现与挂点 ---")]
        // 左手握点
        [Tooltip("左手应该握在哪里")]
        [SerializeField] private Transform _leftHandGoal;
        // 枪口火焰
        [Tooltip("枪口火焰特效")]
        [SerializeField] private ParticleSystem _muzzleFlash;
        // 枪口投射点
        [Tooltip("枪口瞄准参考点")]
        [SerializeField] private Transform _muzzle;
        // 玩家引用
        private BBBCharacterController _player;
        // 实例数据
        private ItemInstance _instance;
        // 配置
        private RangedWeaponSO _config;
        // 射速间隔
        private float _fireRate = 0.1f;
        // 装备状态与时长
        private bool _isEquipping;
        private float _equipEndTime;
        // 上次开火时间
        private float _lastFireTime;
        // 上一帧瞄准状态
        private bool _wasAiming;
        // IK调度
        private bool _ikEnableScheduled;
        private float _ikEnableTimePoint;
        private bool _ikDisableScheduled;
        private float _ikDisableTimePoint;

        // 初始化实例和配置
        public void Initialize(ItemInstance instanceData)
        {
            _instance = instanceData;
            _config = _instance.BaseData as RangedWeaponSO;
            if (_config != null)
            {
                float interval = _config.ShootInterval > 0f ? _config.ShootInterval : _config.FireRate;
                _fireRate = Mathf.Max(0.001f, interval);
            }
        }

        // 装备并设置IK
        public void OnEquipEnter(BBBCharacterController player)
        {
            _player = player;
            _isEquipping = true;
            if (_leftHandGoal != null && _player != null && _player.RuntimeData != null)
            {
                _player.RuntimeData.LeftHandGoal = _leftHandGoal;
                _player.RuntimeData.WantsLeftHandIK = false;
                if (_config != null)
                {
                    _ikEnableScheduled = true;
                    _ikEnableTimePoint = Time.time + _config.EnableIKTime;
                }
                else
                {
                    _player.RuntimeData.WantsLeftHandIK = true;
                }
            }

            // 立刻设置 muzzle，不等瞄准时再设置
            // 这样 FinalIK 始终有有效的引用，避免切装备时 NullRef
            if (_muzzle != null && _player != null && _player.RuntimeData != null)
            {
                _player.RuntimeData.CurrentAimReference = _muzzle;
            }

            float equipAnimDuration = _config != null ? _config.EquipEndTime : 0.5f;
            _equipEndTime = Time.time + equipAnimDuration;
            if (_config != null && _config.EquipAnim != null && _player != null)
            {
                _player.AnimFacade.PlayTransition(_config.EquipAnim, _config.EquipAnimPlayOptions);
            }
        }

        // 每帧更新逻辑
        public void OnUpdateLogic()
        {
            if (_ikEnableScheduled && Time.time >= _ikEnableTimePoint)
            {
                if (_isEquipping)
                {
                    _ikEnableTimePoint = _equipEndTime + 0.001f;
                }
                else
                {
                    _ikEnableScheduled = false;
                    if (_player != null && _player.RuntimeData != null && _player.RuntimeData.CurrentItem == _instance)
                    {
                        _player.RuntimeData.WantsLeftHandIK = true;
                    }
                }
            }
            if (_ikDisableScheduled && Time.time >= _ikDisableTimePoint)
            {
                _ikDisableScheduled = false;
                if (_player != null && _player.RuntimeData != null)
                {
                    var current = _player.RuntimeData.CurrentItem;
                    if (current == null || current.InstanceID == _instance.InstanceID)
                    {
                        _player.RuntimeData.WantsLeftHandIK = false;
                        _player.RuntimeData.LeftHandGoal = null;
                    }
                }
            }
            if (_isEquipping)
            {
                if (Time.time >= _equipEndTime)
                {
                    _isEquipping = false;
                    if (_config != null && _config.EquipIdleAnim != null && _player != null)
                    {
                        _player.AnimFacade.PlayTransition(_config.EquipIdleAnim, _config.EquipIdleAnimOptions);
                    }
                }
                else
                {
                    return;
                }
            }
            bool isAiming = _player != null && _player.RuntimeData != null && _player.RuntimeData.IsAiming;
            if (!_isEquipping && _wasAiming != isAiming)
            {
                if (isAiming)
                {
                    if (_config != null && _config.AimAnim != null && _player != null)
                    {
                        _player.AnimFacade.PlayTransition(_config.AimAnim, _config.AnimPlayOptions);
                    }
                    if (_player != null && _player.RuntimeData != null)
                    {
                        // 瞄准时只改意图，muzzle 已在装备时设置
                        _player.RuntimeData.WantsLookAtIK = true;
                    }
                }
                else
                {
                    if (_config != null && _config.EquipIdleAnim != null && _player != null)
                    {
                        _player.AnimFacade.PlayTransition(_config.EquipIdleAnim, _config.EquipIdleAnimOptions);
                    }
                    if (_player != null && _player.RuntimeData != null)
                    {
                        // 仅改意图，不清理 CurrentAimReference
                        _player.RuntimeData.WantsLookAtIK = false;
                    }
                }
                _wasAiming = isAiming;
            }
            bool isFiring = _player != null && _player.RuntimeData != null &&
                           _player.RuntimeData.CurrentItem == _instance &&
                           _player.RuntimeData.WantsToFire;
            if (isAiming && isFiring)
            {
                TryFire();
            }
        }

        // 强制卸载
        public void OnForceUnequip()
        {
            _isEquipping = false;
            if (_muzzleFlash != null) _muzzleFlash.Stop();

            // IK 清理职责已转移到 EquipmentDriver

            if (_config != null)
            {
                _ikDisableScheduled = true;
                _ikDisableTimePoint = Time.time + _config.DisableIKTime;
            }

            if (_player != null && _player.RuntimeData != null && _player.RuntimeData.CurrentItem == null)
            {
                if (_config != null && _config.UnEquipAnim != null)
                {
                    _player.AnimFacade.PlayTransition(_config.UnEquipAnim, _config.UnEquipAnimPlayOptions);
                }
            }
        }

        public void OnSpawned()
        {
            // 保守复位（避免复用武器时残留）
            _isEquipping = false;
            _wasAiming = false;
            _ikEnableScheduled = false;
            _ikDisableScheduled = false;
            _lastFireTime = 0f;

            if (_muzzleFlash != null) _muzzleFlash.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        public void OnDespawned()
        {
            if (_muzzleFlash != null) _muzzleFlash.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        // 检查冷却并开火
        private void TryFire()
        {
            if (Time.time - _lastFireTime < _fireRate) return;
            _lastFireTime = Time.time;
            if (_muzzleFlash != null) _muzzleFlash.Play();
            if (_config != null && _config.ShootSound != null && _muzzle != null)
            {
                AudioSource.PlayClipAtPoint(_config.ShootSound, _muzzle.position);
            }

            if (_config != null && _config.MuzzleVFXPrefab != null && _muzzle != null)
            {
                GameObject muzzleVFX;
                if (SimpleObjectPoolSystem.Shared != null)
                {
                    muzzleVFX = SimpleObjectPoolSystem.Shared.Spawn(_config.MuzzleVFXPrefab);
                    muzzleVFX.transform.SetPositionAndRotation(_muzzle.position, _muzzle.rotation);
                    muzzleVFX.transform.SetParent(_muzzle, true);
                }
                else
                {
                    muzzleVFX = Object.Instantiate(_config.MuzzleVFXPrefab, _muzzle.position, _muzzle.rotation);
                    muzzleVFX.transform.parent = _muzzle;
                }
            }

            ApplyRecoil();

            if (_config != null && _config.ProjectilePrefab != null && _muzzle != null)
            {
                GameObject proj;
                if (SimpleObjectPoolSystem.Shared != null)
                {
                    proj = SimpleObjectPoolSystem.Shared.Spawn(_config.ProjectilePrefab);
                    proj.transform.SetPositionAndRotation(_muzzle.position, _muzzle.rotation);
                    proj.transform.SetParent(null, true);
                }
                else
                {
                    proj = Object.Instantiate(_config.ProjectilePrefab, _muzzle.position, _muzzle.rotation);
                    proj.transform.parent = null;
                }

                if (proj.TryGetComponent<Rigidbody>(out var rb))
                {
                    rb.velocity = _muzzle.forward * _config.ProjectileSpeed;
                }
                if (proj.TryGetComponent<SimpleProjectile>(out var simple))
                {
                    simple.hitSound = _config.ProjectileHitSound;
                }
            }
        }

        // 应用后坐力
        private void ApplyRecoil()
        {
            if (_player == null || _player.RuntimeData == null || _config == null) return;
            float pitchNoise = Random.Range(-_config.RecoilPitchRandomRange, _config.RecoilPitchRandomRange);
            float yawNoise = Random.Range(-_config.RecoilYawRandomRange, _config.RecoilYawRandomRange);
            float finalPitch = _config.RecoilPitchAngle + pitchNoise;
            float finalYaw = _config.RecoilYawAngle + yawNoise;
            float yawSign = Random.value > 0.5f ? 1f : -1f;
            _player.RuntimeData.ViewPitch -= finalPitch;
            _player.RuntimeData.ViewYaw += yawSign * finalYaw;
            _player.RuntimeData.ViewPitch = Mathf.Clamp(
                _player.RuntimeData.ViewPitch,
                _player.Config.Core.PitchLimits.x,
                _player.Config.Core.PitchLimits.y
            );
        }
    }
}
