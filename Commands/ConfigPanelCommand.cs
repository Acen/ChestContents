using ChestContents.Managers;
using Jotunn.Entities;

namespace ChestContents.Commands
{
    public class ConfigPanelCommand : ConsoleCommand
    {
        private readonly string _name;
        public ConfigPanelCommand(string name = "chestconfig")
        {
            _name = name;
        }
        public override string Name => _name;
        public override string Help => "Open the ChestContents config panel";
        public override bool IsNetwork => false;

        public override void Run(string[] args)
        {
            ChestContentsPlugin.Logger.LogWarning("/chestconfig command run.");
            if (ChestContentsPlugin.ConfigPanelManagerInstance == null)
            {
                ChestContentsPlugin.Logger.LogWarning("Config panel is not available yet. Try again in a few seconds after entering the world.");
                return;
            }
            ChestContentsPlugin.ConfigPanelManagerInstance.ShowPanel();
        }
    }
}

