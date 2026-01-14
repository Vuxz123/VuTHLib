using UnityEngine;

namespace TScript
{
    public class NewBehaviourScript : MonoBehaviour
    {
        [Header("Single Slot Demo")]
        [SerializeField] public Sprite sourceItem;
        [SerializeField] public Sprite slotItem;

        [Header("Inventory Grid Demo")]
        public int rows = 2;
        public int columns = 4;
        [SerializeField] public Sprite[] flatItems;

        void Awake()
        {
            // Ensure flatItems has correct size at edit time as well
            SyncFlatArraySize();
        }

        private void OnValidate()
        {
            SyncFlatArraySize();
        }

        public void SyncFlatArraySize()
        {
            int count = Mathf.Max(0, rows) * Mathf.Max(0, columns);
            if (flatItems == null || flatItems.Length != count)
            {
                var old = flatItems;
                flatItems = new Sprite[count];
                if (old != null)
                {
                    for (int i = 0; i < Mathf.Min(old.Length, flatItems.Length); i++)
                        flatItems[i] = old[i];
                }
            }
        }
    }
}
