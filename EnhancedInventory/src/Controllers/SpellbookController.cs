﻿using EnhancedInventory.Util;
using Kingmaker.UI.MVVM._PCView.ServiceWindows.Spellbook;
using Kingmaker.UI.MVVM._PCView.ServiceWindows.Spellbook.KnownSpells;
using Owlcat.Runtime.UniRx;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UniRx;
using Kingmaker.UnitLogic.Abilities;
using Kingmaker.UI.Common;
using Kingmaker.UI.MVVM._VM.ServiceWindows.Spellbook.KnownSpells;
using Kingmaker.UnitLogic;
using Owlcat.Runtime.UI.Controls.Other;
using Owlcat.Runtime.UI.Controls.Button;
using Kingmaker.UI.MVVM._VM.ServiceWindows.Spellbook.Metamagic;
using HarmonyLib;
using Kingmaker.UI;
using TMPro;
using UnityEngine.UI;
using Kingmaker.UI.MVVM._VM.ServiceWindows.Spellbook.Switchers;
using Kingmaker.UnitLogic.Abilities.Blueprints;
using Kingmaker.UI.MVVM._PCView.ServiceWindows.CharacterInfo.Menu;
using Kingmaker.Items;
using Kingmaker.Blueprints.Items.Components;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Root;
using Kingmaker.EntitySystem.Stats;
using EnhancedInventory.Localization;
using Kingmaker.UI.Group;

namespace EnhancedInventory.Controllers
{
    public class DummyKnownSpellsView : SpellbookKnownSpellsPCView
    {
        public override void BindViewImplementation() { }
        public override void DestroyViewImplementation() { }
    }

    public class SpellbookController : MonoBehaviour
    {
        private SearchBar m_search_bar;

        private IReactiveProperty<Spellbook> m_spellbook;
        private IReactiveProperty<SpellbookLevelVM> m_spellbook_level;
        private IReactiveProperty<AbilityDataVM> m_selected_spell;

        private ScrollRectExtended m_scroll_bar;

        private SpellbookKnownSpellPCView m_known_spell_prefab;
        private SpellbookSpellPCView m_possible_spell_prefab;

        private ToggleWorkaround m_all_spells_checkbox;
        private ToggleWorkaround m_possible_spells_checkbox;
        private ToggleWorkaround m_metamagic_checkbox;
        private Button m_learn_scrolls_button;

        private string m_localized_fort;
        private string m_localized_reflex;
        private string m_localized_will;

        private List<IDisposable> m_handlers = new List<IDisposable>();
        private bool m_deferred_update = true;

        private int m_last_spell_level = -1;

        private void Awake()
        {
            m_search_bar = new SearchBar(transform.Find("MainContainer"), SpellbookStrings.EnterSpellName);
            m_search_bar.GameObject.transform.localScale = new Vector3(0.85f, 0.85f, 1.0f);
            m_search_bar.GameObject.transform.localPosition = new Vector2(-61.0f, 386.0f);
            m_search_bar.DropdownIconObject.SetActive(false);
            m_search_bar.Dropdown.onValueChanged.AddListener(delegate { m_deferred_update = true; m_scroll_bar.ScrollToTop(); });
            m_search_bar.InputField.onValueChanged.AddListener(delegate { m_deferred_update = true; m_scroll_bar.ScrollToTop(); });

            // Setup string options...

            m_localized_fort = LocalizedTexts.Instance.Stats.Entries.First(i => i.Stat == StatType.SaveFortitude).Text;
            m_localized_reflex = LocalizedTexts.Instance.Stats.Entries.First(i => i.Stat == StatType.SaveReflex).Text;
            m_localized_will = LocalizedTexts.Instance.Stats.Entries.First(i => i.Stat == StatType.SaveWill).Text;

            List<string> options = Enum.GetValues(typeof(SpellbookFilter)).Cast<SpellbookFilter>().Select(i => i.ToString()).ToList();
            options[(int)SpellbookFilter.NoFilter] = SpellbookStrings.NoFilter;
            options[(int)SpellbookFilter.TargetsFortitude] = string.Format(SpellbookStrings.FilterTargets, m_localized_fort);
            options[(int)SpellbookFilter.TargetsReflex] = string.Format(SpellbookStrings.FilterTargets, m_localized_reflex);
            options[(int)SpellbookFilter.TargetsWill] = string.Format(SpellbookStrings.FilterTargets, m_localized_will);
            m_search_bar.Dropdown.AddOptions(options);
            m_search_bar.UpdatePlaceholder();

            // The scroll bar is used for resetting the scroll.
            m_scroll_bar = transform.Find("MainContainer/KnownSpells/StandardScrollView").GetComponent<ScrollRectExtended>();

            Transform known_spells_transform = transform.Find("MainContainer/KnownSpells");

            // Grab what we need from the old view then destroy it.
            SpellbookKnownSpellsPCView old_view = known_spells_transform.GetComponent<SpellbookKnownSpellsPCView>();
            m_known_spell_prefab = old_view.m_KnownSpellView;
            m_possible_spell_prefab = old_view.m_PossibleSpellView;
            Destroy(old_view);

            // Make a dummy view that does nothing - we handle the logic in here.
            DummyKnownSpellsView dummy = known_spells_transform.gameObject.AddComponent<DummyKnownSpellsView>();
            dummy.m_KnownSpellView = m_known_spell_prefab;
            dummy.m_PossibleSpellView = m_possible_spell_prefab;
            GetComponentInParent<SpellbookPCView>().m_KnownSpellsView = dummy;

            // Disable the current spell level indicator, it isn't used any more.
            Destroy(transform.Find("MainContainer/Information/CurrentLevel").gameObject);

            // Create button to toggle metamagic.
            GameObject all_spells_button = Instantiate(transform.Find("MainContainer/KnownSpells/Toggle").gameObject, transform.Find("MainContainer/KnownSpells"));
            all_spells_button.name = "ToggleAllSpells";
            all_spells_button.transform.localPosition = new Vector2(501.0f, -405.0f);
            all_spells_button.transform.Find("Label").GetComponent<TextMeshProUGUI>().text = SpellbookStrings.ShowAllSpellLevels;
            m_all_spells_checkbox = all_spells_button.GetComponent<ToggleWorkaround>();
            m_all_spells_checkbox.onValueChanged.AddListener(delegate { m_deferred_update = true; m_scroll_bar.ScrollToTop(); });
            m_all_spells_checkbox.isOn = Main.Settings.SpellbookShowAllSpellsByDefault;

            GameObject metamagic_button = Instantiate(transform.Find("MainContainer/KnownSpells/Toggle").gameObject, transform.Find("MainContainer/KnownSpells"));
            metamagic_button.name = "ToggleMetamagic";
            metamagic_button.transform.localPosition = new Vector2(501.0f, -480.0f);
            metamagic_button.transform.Find("Label").GetComponent<TextMeshProUGUI>().text = SpellbookStrings.ShowMetamagic;
            m_metamagic_checkbox = metamagic_button.GetComponent<ToggleWorkaround>();
            m_metamagic_checkbox.onValueChanged.AddListener(delegate { m_deferred_update = true; m_scroll_bar.ScrollToTop(); });
            m_metamagic_checkbox.isOn = Main.Settings.SpellbookShowMetamagicByDefault;

            GameObject possible_spells_button = Instantiate(transform.Find("MainContainer/KnownSpells/Toggle").gameObject, transform.Find("MainContainer/KnownSpells"));
            possible_spells_button.name = "TogglePossibleSpells";
            possible_spells_button.transform.localPosition = new Vector2(501.0f, -443.0f);
            possible_spells_button.transform.Find("Label").GetComponent<TextMeshProUGUI>().text = SpellbookStrings.ShowUnlearnedSpells;
            m_possible_spells_checkbox = possible_spells_button.GetComponent<ToggleWorkaround>();
            m_possible_spells_checkbox.onValueChanged.AddListener(delegate { m_deferred_update = true; m_scroll_bar.ScrollToTop(); });

            // Hide original; keep it around for mod interop.
            transform.Find("MainContainer/KnownSpells/Toggle").gameObject.SetActive(false);

            // Move the levels display (which is still used for displaying memorized spells).
            Transform levels = transform.Find("MainContainer/Levels");
            levels.GetComponent<HorizontalLayoutGroupWorkaround>().childAlignment = TextAnchor.MiddleLeft;
            levels.localPosition = new Vector2(739.0f, 385.0f);

            // Shamelessly steal a button from the inventory and repurpose it for our nefarious deeds.
            GameObject learn_spells_object = Instantiate(transform.parent.parent.Find("CharacterInfoPCView/CharacterScreen/Menu/Button").gameObject, transform.Find("MainContainer"));
            learn_spells_object.name = "LearnAllSpells";
            learn_spells_object.transform.localPosition = new Vector2(800.0f, -430.0f);

            Transform existing_bg = learn_spells_object.transform.Find("ButtonBackground");
            learn_spells_object.AddComponent<Image>().sprite = existing_bg.GetComponent<Image>().sprite;

            RectTransform rect = learn_spells_object.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(150.0f, 60.0f);

            Destroy(existing_bg.gameObject);
            Destroy(learn_spells_object.transform.Find("Selected").gameObject);
            Destroy(learn_spells_object.GetComponent<CharInfoMenuPCView>());
            Destroy(learn_spells_object.GetComponent<OwlcatMultiButton>());

            m_learn_scrolls_button = learn_spells_object.AddComponent<Button>();
            m_learn_scrolls_button.onClick.AddListener(delegate
            {
                m_deferred_update = true;

                UnitEntityData unit = GroupController.Instance.GetCurrentCharacter();
                foreach (ItemEntity item in GetLearnableScrolls())
                {
                    CopyScroll copy = item.Blueprint.GetComponent<CopyScroll>();
                    copy.Copy(item, unit);
                }
            });

            UpdateLearnScrollButton();
        }

        private void OnEnable()
        {
            m_spellbook = null;
        }

        private void Update()
        {
            m_deferred_update |= m_spellbook == null;

            if (m_spellbook == null)
            {
                Setup();
            }

            if (m_deferred_update)
            {
                foreach (IDisposable handler in m_handlers)
                {
                    handler.Dispose();
                }

                m_handlers.Clear();

                UpdateLearnScrollButton();

                WidgetListMVVM widgets = transform.Find("MainContainer/KnownSpells").GetComponent<WidgetListMVVM>();
                widgets.Clear();

                if (m_spellbook.Value != null && m_spellbook_level.Value != null)
                {
                    DrawKnownSpells(widgets);

                    if (m_possible_spells_checkbox.isOn)
                    {
                        DrawPossibleSpells(widgets);
                    }

                    foreach (SpellbookKnownSpellPCView spell in transform
                        .Find("MainContainer/KnownSpells/StandardScrollView/Viewport/Content")
                        .GetComponentsInChildren<SpellbookKnownSpellPCView>())
                    {
                        if (m_all_spells_checkbox.isOn)
                        {
                            // Event per slot in the prefab to change the selected option.
                            m_handlers.Add(spell.m_Button.OnLeftClickAsObservable().Subscribe(delegate (Unit _)
                            {
                                SelectMemorisationLevel(spell.ViewModel.SpellLevel);
                            }));

                            // Draw the level...
                            if (Main.Settings.SpellbookShowLevelWhenViewingAllSpells)
                            {
                                spell.m_SpellLevelContainer.SetActive(true);
                            }
                        }

                        // If we've chosen to disable metamagic circles, axe them.
                        if (!Main.Settings.SpellbookShowEmptyMetamagicCircles)
                        {
                            for (int i = 0; i < spell.ViewModel.SpellMetamagicFeatures.Count; ++i)
                            {
                                if (!spell.ViewModel.AppliedMetamagicFeatures.Contains(spell.ViewModel.SpellMetamagicFeatures[i]))
                                {
                                    spell.m_MetamagicIcons[i].gameObject.SetActive(false);
                                }
                            }
                        }
                    }
                }

                m_deferred_update = false;
            }
        }

        private bool ShouldShowSpell(BlueprintAbility spell, SpellbookFilter filter)
        {
            string save = spell.LocalizedSavingThrow;

            if (filter == SpellbookFilter.TargetsFortitude ||
                filter == SpellbookFilter.TargetsReflex ||
                filter == SpellbookFilter.TargetsWill)
            {
                if (string.IsNullOrWhiteSpace(save)) return false;
                else if (filter == SpellbookFilter.TargetsFortitude && save.IndexOf(m_localized_fort, StringComparison.OrdinalIgnoreCase) == -1) return false;
                else if (filter == SpellbookFilter.TargetsReflex && save.IndexOf(m_localized_reflex, StringComparison.OrdinalIgnoreCase) == -1) return false;
                else if (filter == SpellbookFilter.TargetsWill && save.IndexOf(m_localized_will, StringComparison.OrdinalIgnoreCase) == -1) return false;
            }

            string text = m_search_bar.InputField.text;

            if (string.IsNullOrWhiteSpace(text))
            {
                return true;
            }
            else if (Main.Settings.SpellbookSearchCriteria.HasFlag(Settings.SpellbookSearchCriteria.SpellName) &&
                spell.Name.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
            else if (Main.Settings.SpellbookSearchCriteria.HasFlag(Settings.SpellbookSearchCriteria.SpellDescription) &&
                spell.Description.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
            else if (Main.Settings.SpellbookSearchCriteria.HasFlag(Settings.SpellbookSearchCriteria.SpellSaves) &&
                save.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
            else if (Main.Settings.SpellbookSearchCriteria.HasFlag(Settings.SpellbookSearchCriteria.SpellSchool) &&
                spell.School.ToString().IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            return false;
        }

        private void Setup()
        {
            // Grab the various state we need...
            SpellbookPCView spellbook_pc_view = GetComponentInParent<SpellbookPCView>();
            m_spellbook = spellbook_pc_view.ViewModel.CurrentSpellbook;
            m_spellbook_level = spellbook_pc_view.ViewModel.CurrentSpellbookLevel;
            m_selected_spell = spellbook_pc_view.ViewModel.CurrentSelectedSpell;

            m_spellbook_level.Subscribe(delegate (SpellbookLevelVM level)
            {
                if (level == null) return;

                // Changing the selected level nothing for our view unless we're viewing all spells.
                if (!m_all_spells_checkbox.isOn || level.Level == 11 || m_last_spell_level == 11)
                {
                    m_deferred_update = true;
                    m_scroll_bar.ScrollToTop();
                }

                m_last_spell_level = level.Level;
            });

            // This event is fired when the metamagic builder is opened or shut.
            spellbook_pc_view.ViewModel.MetamagicBuilderMode.Subscribe(delegate (bool state)
            {
                if (!state) return;

                // If we've been opened, we need to register for the callback every time a new spell is created.
                Action old_action = spellbook_pc_view.ViewModel.SpellbookMetamagicMixerVM.m_OnComplete;
                AccessTools.FieldRef<SpellbookMetamagicMixerVM, Action> field = AccessTools.FieldRefAccess<SpellbookMetamagicMixerVM, Action>(nameof(SpellbookMetamagicMixerVM.m_OnComplete));
                field.Invoke(spellbook_pc_view.ViewModel.SpellbookMetamagicMixerVM) = delegate
                {
                    m_deferred_update = true;

                    if (Main.Settings.SpellbookAutoSwitchToMetamagicTab)
                    {
                        old_action();
                    }
                };
            });

            // This event is fired when changing spellbook or updating the spells inside the spellbook.
            spellbook_pc_view.m_CharacteristicsView.ViewModel.RefreshCommand.ObserveLastValueOnLateUpdate().Subscribe(delegate (Unit _)
            {
                m_deferred_update = true;
                m_scroll_bar.ScrollToTop();
            });

            if (Main.Settings.SpellbookSearchBarFocusWhenOpening)
            {
                m_search_bar.FocusSearchBar();
            }
        }

        private void DrawKnownSpells(WidgetListMVVM widgets)
        {
            List<AbilityDataVM> known_spells = new List<AbilityDataVM>();

            int spellbook_level = m_spellbook_level.Value.Level;

            for (int level = 0; level <= 10; ++level)
            {
                if (!m_all_spells_checkbox.isOn && spellbook_level != 11 && level != spellbook_level) continue;

                foreach (AbilityData spell in UIUtilityUnit.GetKnownSpellsForLevel(level, m_spellbook.Value))
                {
                    if (!m_metamagic_checkbox.isOn && spell.MetamagicData != null) continue;
                    if (spellbook_level == 11 && spell.MetamagicData == null) continue;

                    if (ShouldShowSpell(spell.Blueprint, (SpellbookFilter)m_search_bar.Dropdown.value))
                    {
                        known_spells.Add(new AbilityDataVM(spell, m_spellbook.Value, m_selected_spell));
                    }
                }
            }

            widgets.DrawEntries(known_spells.OrderBy(i => i.SpellLevel).ThenBy(i => i.DisplayName), m_known_spell_prefab);
        }

        private void DrawPossibleSpells(WidgetListMVVM widgets)
        {
            List<BlueprintAbilityVM> possible_spells = new List<BlueprintAbilityVM>();

            int spellbook_level = m_spellbook_level.Value.Level;

            if (spellbook_level != 11)
            {
                for (int level = 0; level <= 9; ++level)
                {
                    if (!m_all_spells_checkbox.isOn && level != spellbook_level) continue;

                    foreach (BlueprintAbility spell in UIUtilityUnit.GetAllPossibleSpellsForLevel(level, m_spellbook.Value))
                    {
                        if (ShouldShowSpell(spell, (SpellbookFilter)m_search_bar.Dropdown.value))
                        {
                            possible_spells.Add(new BlueprintAbilityVM(spell, m_spellbook.Value, spellbook_level));
                        }
                    }
                }
            }

            widgets.DrawEntries(possible_spells.OrderBy(i => i.m_SpellLevel).ThenBy(i => i.DisplayName), m_possible_spell_prefab);
        }

        private void SelectMemorisationLevel(int level)
        {
            Transform levels = transform.Find("MainContainer/Levels");
            levels.GetChild(level).GetComponent<OwlcatMultiButton>().OnLeftClick.Invoke();
        }

        private List<ItemEntity> GetLearnableScrolls()
        {
            List<ItemEntity> ret = new List<ItemEntity>();

            UnitEntityData unit = GroupController.Instance.GetCurrentCharacter();
            foreach (ItemEntity item in UIUtility.GetStashItems())
            {
                CopyScroll scroll = item.Blueprint.GetComponent<CopyScroll>();
                if (scroll != null && scroll.CanCopy(item, unit))
                {
                    ret.Add(item);
                }
            }
            return ret;
        }

        private void UpdateLearnScrollButton()
        {
            List<ItemEntity> learnable_scrolls = GetLearnableScrolls();
            m_learn_scrolls_button.interactable = learnable_scrolls.Count > 0;
            TextMeshProUGUI title_text = m_learn_scrolls_button.transform.Find("MenuTitle").GetComponent<TextMeshProUGUI>();
            title_text.text = string.Format(SpellbookStrings.LearnScrolls, learnable_scrolls.Count);
        }
    }
}
