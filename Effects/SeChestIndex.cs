using ChestContents.Managers;

namespace ChestContents.Effects
{
    public class SeChestIndex : StatusEffect
    {
        public override string GetIconText()
        {
            var indexed = 0;
            try
            {
                indexed = ChestContentsPlugin.ChestInfoDict.Count;
            }
            catch
            {
            }

            return indexed == 1 ? $"{indexed} chest" : $"{indexed} chests";
        }
    }
}