using System.Collections.Generic;
using UnityEngine;
using YanyiSandpan.Domain;

namespace YanyiSandpan.Presentation
{
    // 相邻热区扩大后，重叠部分归属屏幕上最近的阵位，避免绘制顺序抢走输入。
    public sealed class SlotHitArea : MonoBehaviour, ICanvasRaycastFilter
    {
        private int slotId;
        private RectTransform ground;
        private IList<SlotDefinition> slots;

        public void Configure(int id, RectTransform coordinateRoot, IList<SlotDefinition> definitions)
        { slotId = id; ground = coordinateRoot; slots = definitions; }

        public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
        {
            Vector2 local;
            if (ground == null || slots == null || !RectTransformUtility.ScreenPointToLocalPointInRectangle(ground, screenPoint, eventCamera, out local)) return false;
            int nearest = -1;
            float nearestDistance = float.MaxValue;
            foreach (SlotDefinition slot in slots)
            {
                float x = local.x - slot.Position.X;
                float y = (-local.y - slot.Position.Y) * BattleLayout.GroundScale;
                if (Mathf.Abs(x) > 60 || Mathf.Abs(y) > 44) continue;
                float distance = x * x + y * y;
                if (distance < nearestDistance || (Mathf.Approximately(distance, nearestDistance) && slot.Id < nearest))
                { nearest = slot.Id; nearestDistance = distance; }
            }
            return nearest == slotId;
        }
    }
}
