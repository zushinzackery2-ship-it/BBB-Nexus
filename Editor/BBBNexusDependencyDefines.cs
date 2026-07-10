#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace BBBNexus
{
    /// <summary>
    /// 检测可选的第三方依赖项（任何导入方式：UPM/Assets/Plugins/预编译dll）
    /// 通过检查已知类型并切换脚本定义符号。
    ///
    /// 这让可选功能程序集可以使用asmdef defineConstraints来排除编译
    /// 当依赖项不存在时。
    /// </summary>
    [InitializeOnLoad]
    internal static class BBBNexusDependencyDefines
    {
        private const string DefineUar = "BBBNEXUS_HAS_UAR";
        private const string DefineFinalIk = "BBBNEXUS_HAS_FINALIK";
        private const string DefineCinemachine = "BBBNEXUS_HAS_CINEMACHINE";

        static BBBNexusDependencyDefines()
        {
            UpdateDefines();

            // 当Unity重新编译脚本时重新运行
            UnityEditor.Compilation.CompilationPipeline.compilationFinished += _ => UpdateDefines();
        }

        private static void UpdateDefines()
        {
            // 注意：故意使用类型检测。无论导入方式如何都有效（UPM/Assets/Plugins/dll）
            bool hasUar = HasType("UnityEngine.Animations.Rigging.RigBuilder", "UnityEngine.Animations.Rigging");

            // FinalIK作为Plugins下的源脚本导入，最终在Assembly-CSharp-firstpass中。
            // 当BBBNexus还没有自己的asmdef时，单独的可选asmdef无法可靠地同时引用
            // Assembly-CSharp和Assembly-CSharp-firstpass在所有IDE/编译管道中。
            // 所以不自动启用此符号；除非用户手动连接程序集，否则保持FinalIK适配器代码为存根。
            bool hasFinalIk = HasType("RootMotion.FinalIK.AimIK", "Assembly-CSharp-firstpass") || HasType("RootMotion.FinalIK.AimIK", "Assembly-CSharp");

            bool hasCinemachine = HasType("Cinemachine.CinemachineBrain", "Cinemachine");

            var group = EditorUserBuildSettings.selectedBuildTargetGroup;
            SetDefine(group, DefineUar, hasUar);
            SetDefine(group, DefineFinalIk, hasFinalIk);
            SetDefine(group, DefineCinemachine, hasCinemachine);
        }

        private static bool HasType(string fullTypeName, string preferredAssemblyName)
        {
            // 快速路径：如果知道程序集名称则指定它
            if (Type.GetType($"{fullTypeName}, {preferredAssemblyName}") != null)
                return true;

            // 回退：按类型名称搜索已加载的程序集
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    if (asm.GetType(fullTypeName, false) != null)
                        return true;
                }
                catch { }
            }

            return false;
        }

        private static void SetDefine(BuildTargetGroup group, string define, bool enabled)
        {
            var defines = PlayerSettings.GetScriptingDefineSymbolsForGroup(group)
                .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(d => d.Trim())
                .Where(d => !string.IsNullOrEmpty(d))
                .ToList();

            bool has = defines.Contains(define);
            if (enabled && !has)
            {
                defines.Add(define);
                PlayerSettings.SetScriptingDefineSymbolsForGroup(group, string.Join(";", defines));
            }
            else if (!enabled && has)
            {
                defines.RemoveAll(d => d == define);
                PlayerSettings.SetScriptingDefineSymbolsForGroup(group, string.Join(";", defines));
            }
        }
    }
}
#endif