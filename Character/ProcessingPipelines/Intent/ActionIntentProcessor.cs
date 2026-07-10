using UnityEngine;

namespace BBBNexus
{
    /// <summary>
    /// Action 意图处理器：把输入快照里的 ActionPressed 写入黑板。
    /// </summary>
    public sealed class ActionIntentProcessor
    {
        private readonly PlayerRuntimeData _data;

        public ActionIntentProcessor(PlayerRuntimeData data)
        {
            _data = data;
        }
        
        public void Update(in ProcessedInputData input)
        {
            if (input.ActionPressed)
            {
                _data.WantsToAction = true;
            }
        }
    }
}
