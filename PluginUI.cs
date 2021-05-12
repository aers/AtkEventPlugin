using System;
using Dalamud.Plugin;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;

namespace AtkEventPlugin
{
    public unsafe class PluginUI
    {
        private Plugin _plugin;

        private string nodeText = "Input me!";
        private Int32 eventType = 0;
        private Int32 eventParam = 0;

        public PluginUI(Plugin p)
        {
            this._plugin = p;
        }

        private bool visible = true;

        public bool IsVisible
        {
            get { return this.visible; }
            set { this.visible = value; }
        }

        public void Draw()
        {
            if (!IsVisible)
                return;

            if (ImGui.Begin($"{_plugin.Name}", ref visible, ImGuiWindowFlags.AlwaysAutoResize))
            {

                ImGui.InputText("Node Address", ref nodeText, 30);
                ImGui.InputInt("Event Type", ref eventType);
                ImGui.InputInt("Event Param", ref eventParam);

                if (ImGui.Button("Add Event"))
                {
                    try
                    {
                        var address = Convert.ToInt64(nodeText, 16);
                        PluginLog.Log($"add event node: {address:X} type: {eventType} param: {eventParam}");
                        _plugin.eventListener.AddEvent((AtkResNode*) new IntPtr(address).ToPointer(), (ushort) eventType, (uint) eventParam );
                    }
                    catch(FormatException e)
                    {
                        PluginLog.Log("invalid node address");
                    }
                }

                ImGui.Separator();

                if (ImGui.BeginTable("###mainTable", 3))
                {
                    ImGui.TableSetupColumn("Node");
                    ImGui.TableSetupColumn("Type");
                    ImGui.TableSetupColumn("Param");

                    ImGui.TableHeadersRow();

                    ImGui.TableNextColumn();

                    foreach (var re in _plugin.eventListener.eventMap)
                    {
                        ImGui.Text($"{(long)re.Value.source:X}");
                        ImGui.TableNextColumn();
                        ImGui.Text($"{re.Value.type}");
                        ImGui.TableNextColumn();
                        ImGui.Text($"{re.Value.param}");
                        ImGui.TableNextColumn();
                    }

                    ImGui.EndTable();
                }

                ImGui.End();
            }
        }
    }
}
