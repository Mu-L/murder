using ImGuiNET;
using System.Collections.Immutable;
using Murder.ImGuiExtended;
using Murder.Diagnostics;
using Microsoft.Xna.Framework.Input;
using Murder.Editor.ImGuiExtended;
using Murder.Prefabs;

namespace Murder.Editor.CustomEditors
{
    internal partial class WorldAssetEditor
    {
        private void DrawEntitiesEditor()
        {
            GameLogger.Verify(_asset is not null && Stages.ContainsKey(_asset.Guid));

            const string popupName = "New group";
            if (ImGuiHelpers.IconButton('\uf65e', $"add_group"))
            {
                _groupName = string.Empty;
                ImGui.OpenPopup(popupName);
            }

            DrawCreateOrRenameGroupPopup(popupName);

            ImGui.SameLine();
            if (CanAddInstance && ImGuiHelpers.IconButton('\uf234', $"add_entity_world"))
            {
                ImGui.OpenPopup("Add entity");
            }

            if (ImGui.BeginPopup("Add entity"))
            {
                if (SearchBox.SearchInstantiableEntities() is Guid asset)
                {
                    EntityInstance instance = EntityBuilder.CreateInstance(asset);
                    AddInstance(instance);

                    ImGui.CloseCurrentPopup();
                }

                ImGui.EndPopup();
            }

            _selecting = -1;

            HashSet<Guid> groupedEntities = DrawEntityGroups();

            if (TreeEntityGroupNode("All Entities", Game.Profile.Theme.White, icon: '\uf500', flags: ImGuiTreeNodeFlags.DefaultOpen))
            {
                DrawEntityList(Instances, groupedEntities);

                ImGui.TreePop();
            }
        }
        
        /// <summary>
        /// Draw all folders with entities. Returns a list with all the entities grouped within those folders.
        /// </summary>
        protected virtual HashSet<Guid> DrawEntityGroups()
        {
            HashSet<Guid> groupedEntities = new();

            ImmutableDictionary<string, HashSet<Guid>> folders = _world?.FetchFolders() ?? ImmutableDictionary<string, HashSet<Guid>>.Empty;
            foreach ((string name, HashSet<Guid> entities) in folders)
            {
                groupedEntities.Concat(entities);

                if (TreeEntityGroupNode(name, Game.Profile.Theme.Yellow))
                {
                    if (ImGuiHelpers.DeleteButton($"Delete_group_{name}"))
                    {
                        _world?.DeleteGroup(name);
                    }

                    ImGui.SameLine();

                    string popupName = $"Rename group##{name}";
                    if (ImGuiHelpers.IconButton('\uf304', $"rename_group_{name}"))
                    {
                        _groupName = name;
                        
                        ImGui.OpenPopup(popupName);
                    }
                    
                    DrawCreateOrRenameGroupPopup(popupName, previousName: name);

                    ImGui.SameLine();
                    if (CanAddInstance && ImGuiHelpers.IconButton('\uf234', $"add_entity_in_{name}"))
                    {

                    }

                    DrawEntityList(entities.ToList());
                    ImGui.TreePop();
                }
            }

            return groupedEntities;
        }

        private bool TreeEntityGroupNode(string name, System.Numerics.Vector4 textColor, char icon = '\ue1b0', ImGuiTreeNodeFlags flags = ImGuiTreeNodeFlags.None) =>
            ImGuiHelpers.TreeNodeWithIconAndColor(
                icon: icon,
                label: name,
                flags: ImGuiTreeNodeFlags.Framed | ImGuiTreeNodeFlags.FramePadding | flags,
                text: textColor, 
                background: Game.Profile.Theme.BgFaded, 
                active: Game.Profile.Theme.Bg);

        private void DrawEntityList(IList<Guid> entities, HashSet<Guid>? skipEntities = null)
        {
            GameLogger.Verify(_asset is not null);

            foreach (Guid entity in entities)
            {
                // Entity was already shown within a folder.
                if (skipEntities != null && skipEntities.Contains(entity))
                {
                    continue;
                }

                if (ImGuiHelpers.DeleteButton($"Delete_{entity}"))
                {
                    DeleteInstance(parent: null, entity);
                }

                ImGui.SameLine();

                ImGui.PushID($"Entity_bar_{entity}");

                bool isSelected = Stages[_asset.Guid].IsSelected(entity);

                if (ImGui.Selectable(TryFindInstance(entity)?.Name ?? "<?>", isSelected))
                {
                    _selecting = Stages[_asset.Guid].SelectEntity(entity, select: true);
                    if (_selecting is -1)
                    {
                        // Unable to find the entity. This probably means that it has been deactivated.
                        _selectedAsset = entity;
                    }
                    else
                    {
                        _selectedAsset = null;
                    }
                }

                ImGui.PopID();
            }
        }
        
        private string _groupName = "";

        private void DrawCreateOrRenameGroupPopup(string popupName, string? previousName = null)
        {
            if (_world is null)
            {
                GameLogger.Warning("Unable to add group without a world!");
                return;
            }

            bool isRename = previousName is not null;

            if (ImGui.BeginPopup(popupName))
            {
                if (!isRename)
                {
                    ImGui.Text("What's the new group name?");
                }
                
                ImGui.InputText("##group_name", ref _groupName, 128, ImGuiInputTextFlags.AutoSelectAll);

                string buttonLabel = isRename ? "Rename" : "Create";
                
                if (!string.IsNullOrWhiteSpace(_groupName) && !_world.FetchFolders().ContainsKey(_groupName))
                {
                    if (ImGui.Button(buttonLabel) || Game.Input.Pressed(Keys.Enter))
                    {
                        if (previousName is not null)
                        {
                            _world.RenameGroup(previousName, _groupName);
                        }
                        else
                        {
                            _world.AddGroup(_groupName);
                        }

                        ImGui.CloseCurrentPopup();
                    }
                }
                else
                {
                    ImGuiHelpers.DisabledButton(buttonLabel);
                    ImGuiHelpers.HelpTooltip("Unable to add group with duplicate or empty names.");
                }

                ImGui.EndPopup();
            }
        }
    }
}