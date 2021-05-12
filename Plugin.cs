using AtkEventPlugin.Attributes;
using Dalamud.Plugin;
using System;

namespace AtkEventPlugin
{
    public class Plugin : IDalamudPlugin
    {
        public DalamudPluginInterface pluginInterface;
        private PluginCommandManager<Plugin> commandManager;
        private Configuration config;
        private PluginUI ui;
        public PluginAtkEventListener eventListener;

        public string Name => "AtkEventPlugin";

        public void Initialize(DalamudPluginInterface pluginInterface)
        {
            this.pluginInterface = pluginInterface;

            this.config = (Configuration)this.pluginInterface.GetPluginConfig() ?? new Configuration();
            this.config.Initialize(this.pluginInterface);

            this.ui = new PluginUI(this);
            this.pluginInterface.UiBuilder.OnBuildUi += this.ui.Draw;

            this.commandManager = new PluginCommandManager<Plugin>(this, this.pluginInterface);

            this.eventListener = new PluginAtkEventListener(this);
            this.eventListener.Initialize();
        }

        [Command("/aep")]
        [HelpMessage("Show AtkEventPlugin UI.")]
        public void ShowUICommand(string command, string args)
        {
            this.ui.IsVisible = true;
        }

        #region IDisposable Support
        protected virtual void Dispose(bool disposing)
        {
            if (!disposing) return;

            this.eventListener.Dispose();

            this.commandManager.Dispose();

            this.pluginInterface.SavePluginConfig(this.config);

            this.pluginInterface.UiBuilder.OnBuildUi -= this.ui.Draw;

            this.pluginInterface.Dispose();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
