using SML;
using UnityEngine;
using HarmonyLib;
using System.Text.RegularExpressions;
using System.Linq;
using Server.Shared.State.Chat;
using System;
using Server.Shared.State;
using Services;
using Home.Shared;
using Newtonsoft.Json;
using Game.Interface;
using System.Collections.Generic;
using Mentions.Providers;
using System.IO;
using Mentions;
using Server.Shared.Extensions;

namespace Main
{
    [Mod.SalemMod]
    public class Main
    {

        public static void Start()
        {
            Debug.Log("Working?");

        }
    }
    [DynamicSettings]
    public class Settings
    {
        public ModSettings.DropdownSetting SelectedSoundpack
        {
            get
            {
                ModSettings.DropdownSetting SelectedSoundpack = new()
                {
                    Name = "Selected Modded Mentions",
                    Description = "The file used for the mentions.",
                    Options = GetOptions(),
                    AvailableInGame = true,
                    Available = true,
                };
                return SelectedSoundpack;
            }
        }
        public List<string> GetOptions()
        {
            List<string> strings = new(){
            "No Modded Mentions"
        };
            string dir = Path.Combine(Directory.GetCurrentDirectory(), "SalemModLoader", "ModFolders", "Mentions Info", "Modded");
            if (!Directory.Exists(dir)) return strings;
            Directory.GetFiles(dir).ForEach(file =>
            {
                strings.Add(Path.GetFileNameWithoutExtension(file));
            });
            return strings;
        }
    }

    [Mod.SalemMenuItem]
    class ModAction
    {
        static protected readonly List<Role> ExcludedRoles = new List<Role>
        {
            Role.NONE,
            Role.UNKNOWN,
            Role.ANONYMOUS_VOTES,
            Role.FAST_MODE,
            Role.GHOST_TOWN,
            Role.HANGMAN,
            Role.KILLER_ROLES_HIDDEN,
            Role.NO_TOWN_HANGED,
            Role.ONE_TRIAL_PER_DAY,
            Role.ROLES_ON_DEATH_HIDDEN,
            Role.SLOW_MODE,
            Role.TOWN_TRAITOR,
            Role.VIP,
            Role.ROLE_COUNT
        };
        public static void CreateStuff()
        {
            MentionsConstructor constructor = new()
            {
                roleMentions = new List<RoleMentionInfo>(),
                keywordsMentions = null
            };
            List<RoleInfo> list = new(Service.Game.Roles.roleInfos);
            list.Sort((RoleInfo a, RoleInfo b) => string.Compare(a.role.ToDisplayString(), b.role.ToDisplayString(), StringComparison.Ordinal));
            foreach (RoleInfo roleInfo in list)
            {
                if (ExcludedRoles.Contains(roleInfo.role)) continue;
                RoleMentionInfo roleMentionInfo = new()
                {
                    id = (int)roleInfo.role,
                    baseName = roleInfo.role.ToDisplayString(),
                    matches = new List<string>()
                };
                roleMentionInfo.matches.Add(roleMentionInfo.baseName);
                if (roleInfo.shortRoleName.Length > 0)
                {
                    if (!roleInfo.shortRoleName.Contains(",#")) roleMentionInfo.matches.Add(roleInfo.shortRoleName);
                    else
                    {

                        List<string> split = roleInfo.shortRoleName.Split(new string[] { ",#" }, StringSplitOptions.None).ToList();
                        roleMentionInfo.matches.AddRange(split);
                    }
                }
                constructor.roleMentions.Add(roleMentionInfo);
            }
            if (!ModSettings.GetBool("Modify keywords too.", "JAN.rolementionsmanager")) goto CreateFile;
            List<KeywordInfo> list2 = new(Service.Game.Keyword.keywordInfo);
            list2.Sort((KeywordInfo a, KeywordInfo b) => string.Compare(Service.Home.LocalizationService.GetLocalizedString(a.KeywordKey), Service.Home.LocalizationService.GetLocalizedString(b.KeywordKey), StringComparison.Ordinal));
            List<KeywordsMentionInfo> keywordsMentionInfo = new();
            foreach (KeywordInfo keywordInfo in list2)
            {

                KeywordsMentionInfo keywordMentionInfo = new()
                {
                    id = keywordInfo.KeywordId,
                    baseName = Service.Home.LocalizationService.GetLocalizedString(keywordInfo.KeywordKey),
                    matches = new List<string>()
                };
                keywordMentionInfo.matches.Add(keywordMentionInfo.baseName);
                keywordsMentionInfo.Add(keywordMentionInfo);
            }
            constructor.keywordsMentions = keywordsMentionInfo;
        CreateFile:
            string baseJson = JsonConvert.SerializeObject(constructor, Formatting.Indented);
            string directoryPath = Path.Combine(Directory.GetCurrentDirectory(), "SalemModLoader", "ModFolders", "Mentions Info");
            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
                Directory.CreateDirectory(Path.Combine(directoryPath, "Base"));
                Directory.CreateDirectory(Path.Combine(directoryPath, "Modded"));
                File.Create(Path.Combine(directoryPath, "Modded", "ModdedOrganization.json")).Close();
                File.WriteAllText(Path.Combine(directoryPath, "Modded", "ModdedOrganization.json"), baseJson);
            }
            if (!File.Exists(Path.Combine(directoryPath, "Base", "BaseOrganization.json"))) File.Create(Path.Combine(directoryPath, "Base", "BaseOrganization.json")).Close();
            File.WriteAllText(Path.Combine(directoryPath, "Base", "BaseOrganization.json"), baseJson);
            if (Environment.OSVersion.Platform == PlatformID.MacOSX || Environment.OSVersion.Platform == PlatformID.Unix)
            {
                System.Diagnostics.Process.Start("open", "\"" + directoryPath + "\""); //code stolen from tuba
            }
            else
            {
                Application.OpenURL("file://" + directoryPath);
            }
        }
        public static Mod.SalemMenuButton menuButtonName = new()
        {
            Label = "Dump Mentions",
            Icon = FromResources.LoadSprite("RoleMentionsManager.resources.images.modaction.png"),
            OnClick = CreateStuff
        };
    }


    [HarmonyPatch(typeof(SharedMentionsProvider), "Build")]
    class ProcessModdedText
    {
        [HarmonyPrefix]
        public static bool Prefix(ref RebuildMentionTypesFlag rebuildMentionTypesFlag, SharedMentionsProvider __instance)
        {
            Debug.LogWarning("This works :thumb:");
            bool modKeywords = ModSettings.GetBool("Modify keywords too.", "JAN.rolementionsmanager");
            if (!rebuildMentionTypesFlag.HasFlag(RebuildMentionTypesFlag.ROLES) && !(rebuildMentionTypesFlag.HasFlag(RebuildMentionTypesFlag.KEYWORDS) && modKeywords)) return true;
            string directoryPath = Path.Combine(Directory.GetCurrentDirectory(), "SalemModLoader", "ModFolders", "Mentions Info");
            string file = ModSettings.GetString("Selected Modded Mentions", "JAN.rolementionsmanager");
            if (file == "No Modded Mentions") return true;
            if (!Directory.Exists(directoryPath)) return true;
            if (!File.Exists(Path.Combine(directoryPath, "Modded", file + ".json"))) { ModSettings.SetString("Selected Modded Mentions", "No Modded Mentions", "JAN.rolementionsmanager"); return true; }
            bool flag = rebuildMentionTypesFlag.HasFlag(RebuildMentionTypesFlag.ROLES);
            bool flag2 = rebuildMentionTypesFlag.HasFlag(RebuildMentionTypesFlag.KEYWORDS);
            bool flag3 = rebuildMentionTypesFlag.HasFlag(RebuildMentionTypesFlag.PLAYERS);

            MentionsConstructor constructor = new();
            string json = File.ReadAllText(Path.Combine(directoryPath, "Modded", file + ".json"));
            constructor = JsonConvert.DeserializeObject<MentionsConstructor>(json);

            __instance._useColors = Service.Home.UserService.Settings.MentionsUseColorsEnabled;
            __instance._playerEffects = Service.Home.UserService.Settings.MentionsPlayerEffects;
            __instance._roleEffects = Service.Home.UserService.Settings.MentionsRoleEffects;
            if (flag && flag2 && flag3)
            {
                __instance.MentionTokens.Clear();
                __instance.MentionInfos.Clear();
            }

            else
            {
                if (flag)
                {
                    __instance.MentionTokens.RemoveAll((MentionToken m) => m.mentionTokenType == MentionToken.MentionTokenType.ROLE);
                    __instance.MentionInfos.RemoveAll((MentionInfo m) => m.mentionInfoType == MentionInfo.MentionInfoType.ROLE);
                }
                if (flag2 && modKeywords)
                {
                    __instance.MentionTokens.RemoveAll((MentionToken m) => m.mentionTokenType == MentionToken.MentionTokenType.KEYWORD);
                    __instance.MentionInfos.RemoveAll((MentionInfo m) => m.mentionInfoType == MentionInfo.MentionInfoType.KEYWORD);
                }
            }
            if (flag)
            {
                List<RoleInfo> list = new(Service.Game.Roles.roleInfos);
                int num = 0;
                foreach (RoleMentionInfo roleMentionInfo in constructor.roleMentions)
                {
                    RoleInfo roleInfo = list.First(x => (int)x.role == roleMentionInfo.id);
                    string text = roleInfo.role.ToDisplayString();
                    string encodedText = string.Format("[[#{0}]]", (int)roleInfo.role);
                    string text2 = (__instance._roleEffects == 1) ? string.Format("<sprite=\"RoleIcons\" name=\"Role{0}\">", (int)roleInfo.role) : string.Empty;
                    string text3 = __instance._useColors ? (roleInfo.role.ToColorizedDisplayString().Replace(roleInfo.role.ToDisplayString(), text) ?? "") : (text ?? "");
                    string text4 = string.Format("{0}{1}<link=\"r{2}\">{3}<b>{4}</b></link>{5}", new object[]
                    {
                            __instance.styleTagOpen,
                            __instance.styleTagFont,
                            (int)roleInfo.role,
                            text2,
                            text3,
                            __instance.styleTagClose
                    });
                    MentionInfo mentionInfo = new MentionInfo
                    {
                        mentionInfoType = MentionInfo.MentionInfoType.ROLE,
                        richText = text4,
                        encodedText = encodedText,
                        hashCode = text4.ToLower().GetHashCode(),
                        humanText = "#" + text.ToLower()
                    };
                    __instance.MentionInfos.Add(mentionInfo);

                    __instance.MentionTokens.Add(new MentionToken
                    {
                        mentionTokenType = MentionToken.MentionTokenType.ROLE,
                        match = "#" + roleMentionInfo.matches[0],
                        mentionInfo = mentionInfo,
                        priority = num
                    });
                    if (roleMentionInfo.matches.Count > 1)
                    {
                        string shortName = "";
                        foreach (string match in roleMentionInfo.matches)
                        {
                            if(match == roleMentionInfo.matches[0]) continue;
                           shortName += ",#"+match; 
                        }
                        shortName = shortName.Remove(0,2);
                        __instance.MentionTokens.Add(new MentionToken
                            {
                                mentionTokenType = MentionToken.MentionTokenType.ROLE,
                                match = "#" + shortName,
                                mentionInfo = mentionInfo,
                                priority = num
                            });
                    }
                    else
                        __instance.MentionTokens.Add(new MentionToken
                        {
                            mentionTokenType = MentionToken.MentionTokenType.ROLE,
                            match = "#" + roleMentionInfo.matches[0],
                            mentionInfo = mentionInfo,
                            priority = num
                        });
                    num++;

                }
                flag = false;
            }
            if (flag2 && modKeywords && constructor.keywordsMentions != null)
            {
                flag = false;
                List<KeywordInfo> list2 = new(Service.Game.Keyword.keywordInfo);
                int num = 0;
                foreach (KeywordsMentionInfo keywordMentionInfo in constructor.keywordsMentions)
                {
                    KeywordInfo keywordInfo = list2.First(x => x.KeywordId == keywordMentionInfo.id);
                    string text5 = __instance.l10n(keywordInfo.KeywordKey);
                    string encodedText2 = string.Format("[[:{0}]]", keywordInfo.KeywordId);
                    string text6 = "#007AFF";
                    string text7 = __instance._useColors ? string.Concat(new string[]
                    {
                        "<color=",
                        text6,
                        "><b>",
                        text5,
                        "</b></color>"
                    }) : ("<b>" + text5 + "</b>");
                    string text8 = string.Format("{0}{1}<link=\"k{2}\"><b>{3}</b></link>{4}", new object[]
                    {
                        __instance.styleTagOpen,
                        __instance.styleTagFont,
                        keywordInfo.KeywordId,
                        text7,
                        __instance.styleTagClose
                    });
                    MentionInfo mentionInfo2 = new MentionInfo
                    {
                        mentionInfoType = MentionInfo.MentionInfoType.KEYWORD,
                        richText = text8,
                        encodedText = encodedText2,
                        hashCode = text8.ToLower().GetHashCode(),
                        humanText = ":" + text5.ToLower()
                    };
                    __instance.MentionInfos.Add(mentionInfo2);
                    __instance.MentionTokens.Add(new MentionToken
                        {
                            mentionTokenType = MentionToken.MentionTokenType.KEYWORD,
                            match = keywordMentionInfo.matches[0],
                            mentionInfo = mentionInfo2,
                            priority = num
                        });
                    if (keywordMentionInfo.matches.Count > 1)
                    {
                        string shortName = "";
                        foreach (string match in keywordMentionInfo.matches)
                        {
                            if(match == keywordMentionInfo.matches[0]) continue;
                           shortName += ",:"+match; 
                        }
                        shortName = shortName.Remove(0,2);
                        __instance.MentionTokens.Add(new MentionToken
                            {
                                mentionTokenType = MentionToken.MentionTokenType.KEYWORD,
                                match = ":" + shortName,
                                mentionInfo = mentionInfo2,
                                priority = num
                            });
                    }
                    else
                        __instance.MentionTokens.Add(new MentionToken
                        {
                            mentionTokenType = MentionToken.MentionTokenType.KEYWORD,
                            match = ":" + keywordMentionInfo.matches[0],
                            mentionInfo = mentionInfo2,
                            priority = num
                        });
                    num++;
                }
                flag2 = false;
            }
            if (flag2 && flag3)
            {
                rebuildMentionTypesFlag = RebuildMentionTypesFlag.KEYWORDS | RebuildMentionTypesFlag.PLAYERS;
                return true;
            }
            else if (flag2)
            {
                rebuildMentionTypesFlag = RebuildMentionTypesFlag.KEYWORDS;
                return true;
            }
            else if (flag3)
            {
                rebuildMentionTypesFlag = RebuildMentionTypesFlag.PLAYERS;
                return true;
            }
            return false;
        }
    }
    class MentionsConstructor
    {
        public List<RoleMentionInfo> roleMentions = null;
        public List<KeywordsMentionInfo> keywordsMentions = null;
    }
    class RoleMentionInfo
    {
        public int id;
        public string baseName;
        public List<string> matches;
    }
    class KeywordsMentionInfo
    {
        public int id;
        public string baseName;
        public List<string> matches;
    }
}