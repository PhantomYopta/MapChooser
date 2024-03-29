using System.Text.Json;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Entities;
using CounterStrikeSharp.API.Modules.Menu;
using CounterStrikeSharp.API.Modules.Timers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace MapChooser;

public class MapChooser : BasePlugin
{
    public override string ModuleName => "MapChooser";
    public override string ModuleVersion => "2.0";
    public override string ModuleAuthor => "phantom";

    private UsersSettings[] _users = new UsersSettings[65];
    private Config _config;
    private ChatMenu _menuSelectMap;
    
    private Dictionary<string, List<string>> _categoryMaps = new();
    private Dictionary<string, int> _selectMapCount = new();

    private List<string> _mapsList = new();
    private Dictionary<string, List<string>> _nominateMap = new();
    private List<string> _playedMap = new();

    private int _votedRtv;
    private int _playedMapsCount;
    private int _maxrounds = 0;
    
    private string? _nextmap;

    private double _timeLimit;
    private double _cTimeLimit;

    private bool _isStartVote;
    private bool _isExtendMap;
    private bool _canRockTheVote;

    public override void Load(bool hotReload)
    {
        _config = LoadConfig();
        ReadJson();

        RegisterListener<Listeners.OnClientConnected>((slot =>
                {
                    var player = Utilities.GetPlayerFromSlot(slot);
                    if (player.IsBot) return;
                    _users[slot + 1] = new UsersSettings { IsNominated = false, IsRtv = false, NominateMap = null };
                }
            ));
        
        RegisterEventHandler<EventRoundEnd>(((@event, info) =>
        {
            if (_maxrounds <= 0) return HookResult.Continue;

            _maxrounds--;
            
            if(_maxrounds == _config.VotingRoundThreshold)
                StartVoteChangeMap();
            return HookResult.Continue;
        }));
        
        RegisterListener<Listeners.OnMapStart>((name =>
        {
            _timeLimit = 0;
            _votedRtv = 0;
            _nextmap = null;
            _isStartVote = false;
            _isExtendMap = false;
            _selectMapCount.Clear();
            _nominateMap.Clear();

            if (_mapsList.Count != 0)
                _nominateMap.TryAdd("None", new List<string>());

            AddTimer(1.0f,
                () =>
                {
                    _maxrounds = ConVar.Find("mp_maxrounds")!.GetPrimitiveValue<int>();
                    _cTimeLimit = ConVar.Find("mp_timelimit")!.GetPrimitiveValue<float>();
                    if (!(_cTimeLimit > 0)) return;
                    _timeLimit = ConVar.Find("mp_timelimit")!.GetPrimitiveValue<float>() * 60.0f +
                                 Server.EngineTime;
                    AddTimer(
                        (ConVar.Find("mp_timelimit")!.GetPrimitiveValue<float>() - _config.StartVoteChangeMap) *
                        60.0f,
                        () =>
                        {
                            if (!_isStartVote && _nextmap == null)
                            {
                                StartVoteChangeMap();
                            }
                        });
                });
            if (_config.RtvTime > 0)
            {
                _canRockTheVote = true;
                AddTimer(_config.RtvTime, () => _canRockTheVote = false);
            }


            _playedMap.Add(name);
            _playedMapsCount++;
            if (_playedMapsCount == _config.PlayedMaps)
            {
                _playedMap.RemoveAt(0);
                _playedMapsCount = 0;
            }
        }));

        RegisterListener<Listeners.OnMapEnd>((() =>
        {
            if (_nextmap == null) return;

            ChangeMap(_nextmap);
        }));

        RegisterListener<Listeners.OnClientDisconnectPost>((slot =>
        {
            var player = Utilities.GetPlayerFromSlot(slot);
            if (player.IsBot) return;
            if (_users[player.Index].IsRtv) _votedRtv--;
            var countPlayers = _users.Count(p => p != null) - 1;
            var countVote = (int)(countPlayers * _config.Needed) == 0 ? 1 : (int)(countPlayers * _config.Needed);

            if (countVote == _votedRtv && countVote != 0)
            {
                StartVoteChangeMap(true);
            }

            _users[slot + 1] = null!;
        }));

        AddCommand("css_rtv", "", OnCommandRtv);
        AddCommand("css_nominate", "", OnCommandNominate);
        AddCommand("css_timeleft", "", OnCommandTimeleft);
        AddCommand("css_nextmap", "", OnCommandNextMap);
        AddCommand("css_revote", "", OnCommandRevote);
    }

    private void OnCommandRevote(CCSPlayerController? player, CommandInfo commandinfo)
    {
        if(player == null) return;
        
        if(!_isStartVote || _users[player.Index].SelectMap == null) return;
        _selectMapCount[_users[player.Index].SelectMap!]--;
        ChatMenus.OpenMenu(player, _menuSelectMap);
    }

    private void OnCommandNextMap(CCSPlayerController? player, CommandInfo commandinfo)
    {
        if (player == null) return;

        player.PrintToChat(_nextmap == null
            ? Localizer["nextmap_unsuccessfully"]
            : Localizer["nextmap_successfully", _nextmap]);
    }

    private void OnCommandTimeleft(CCSPlayerController? player, CommandInfo commandinfo)
    {
        if (player == null) return;
        if(_cTimeLimit > 0)
        {
            var time = _timeLimit - Server.EngineTime;
            var hours = (int)(time / 3600);
            var minutes = (int)((time % 3600) / 60);
            var seconds = (int)(time % 60);

            if (time > 0.0)
                player.PrintToChat(Localizer["timeleft", $"{hours:D2}:{minutes:D2}:{seconds:D2}"]);
        }
        else
        {
            player.PrintToChat(Localizer["timeleft_round", _maxrounds]);
        }
    }

    private void OnCommandNominate(CCSPlayerController? player, CommandInfo commandinfo)
    {
        if (player == null) return;
        if (_nominateMap.Count == 7)
        {
            player.PrintToChat(Localizer["nominate_map_full"]);
            return;
        }

        var menu = new ChatMenu(Localizer["nominate"]);
        if (_mapsList.Count == 0)
        {
            foreach (var keyValuePair in _categoryMaps)
            {
                menu.AddMenuOption(keyValuePair.Key, OnMenuNominateMaps);
            }
        }
        else
        {
            foreach (var maps in _mapsList)
            {
                menu.AddMenuOption(maps, (controller, option) => OnNominateMap(controller, option, "None"));
            }
        }

        ChatMenus.OpenMenu(player, menu);
    }

    private void OnCommandRtv(CCSPlayerController? player, CommandInfo commandinfo)
    {
        if (player == null) return;
        if (_canRockTheVote || _isStartVote)
        {
            player.PrintToChat(Localizer["can_rtv"]);
            return;
        }

        var user = _users[player.Index]!;
        if (user.IsRtv)
        {
            player.PrintToChat(Localizer["rtv_unsuccessfully"]);
            return;
        }

        var countPlayers = _users.Count(p => p != null);
        var countVote = (int)(countPlayers * _config.Needed) == 0 ? 1 : (int)(countPlayers * _config.Needed);
        user.IsRtv = true;
        _votedRtv++;
        Server.PrintToChatAll(Localizer["rtv_successfully", player.PlayerName, _votedRtv, countVote]);
        if (countVote == _votedRtv)
        {
            StartVoteChangeMap(true);
        }
    }

    private void OnMenuNominateMaps(CCSPlayerController player, ChatMenuOption info)
    {
        var menu = new ChatMenu(Localizer["nominate"]);
        foreach (var maps in _categoryMaps[info.Text])
        {
            if (_nominateMap.ContainsKey(info.Text))
            {
                if (_nominateMap[info.Text].Contains(maps))
                {
                    menu.AddMenuOption(maps + Localizer["selected_map"], null!, true);
                    continue;
                }
            }

            if (_playedMap.Contains(maps))
            {
                menu.AddMenuOption(maps + Localizer["played_map"], null!, true);
                continue;
            }

            menu.AddMenuOption(maps, (controller, option) => OnNominateMap(controller, option, info.Text));
        }

        ChatMenus.OpenMenu(player, menu);
    }

    private void OnNominateMap(CCSPlayerController player, ChatMenuOption option, string category)
    {
        if (_nominateMap.Count == 7) return;
        Server.PrintToChatAll(Localizer["nominate_player", player.PlayerName, option.Text]);
        if (_users[player.Index].NominateMap != null)
            _nominateMap.Remove(_users[player.Index].NominateMap!);
        if (_mapsList.Count == 0)
        {
            if (_nominateMap.TryGetValue(category, out var value))
                value.Add(option.Text);
            else
                _nominateMap.TryAdd(category, new List<string>() { option.Text });
        }
        else
            _nominateMap["None"].Add(option.Text);


        _users[player.Index].NominateMap = option.Text;
    }

    private void ReadJson()
    {
        var configPath = Path.Combine(ModuleDirectory, "maps.json");
        var json = File.ReadAllText(configPath);
        var settings = JObject.Parse(json);

        if (settings["Category"] == null)
        {
            var mapsProperty = settings["Maps"];
            foreach (var map in mapsProperty)
            {
                _mapsList.AddRange(map["workshop"].Select(jt => jt.ToString()));
                _mapsList.AddRange(map["map"].Select(jt => jt.ToString()));
            }
        }
        else
        {
            foreach (var category in settings["Category"])
            {
                var categoryName = category.ToString();
                _categoryMaps.TryAdd(categoryName, new List<string>());
                foreach (var map in settings["Settings"][categoryName])
                {
                    _categoryMaps[categoryName].AddRange(map["workshop"].Select(jt => jt.ToString()));
                    _categoryMaps[categoryName].AddRange(map["map"].Select(jt => jt.ToString()));
                }
            }
        }
    }

    private void StartVoteChangeMap(bool forced = false)
    {
        if (_isStartVote) return;

        _isStartVote = true;
        var menu = new ChatMenu("MapChooser");
        switch (forced)
        {
            case true when _nextmap != null:
                ChangeMap(_nextmap);
                return;
            case true when !_isExtendMap:
                menu.AddMenuOption(Localizer["extend_map"], OnSelectCategory);
                break;
        }

        if (_mapsList.Count == 0)
        {
            foreach (var categoryMap in _categoryMaps)
            {
                menu.AddMenuOption(categoryMap.Key, OnSelectCategory);
            }

            AddTimer(_config.TimeVote, () =>
            {
                var mostUsedMap = (string?)null;
                if (_selectMapCount.Count != 0)
                {
                    var maxCount = 0;
                    foreach (KeyValuePair<string, int> pair in _selectMapCount)
                    {
                        if (pair.Value <= maxCount) continue;

                        mostUsedMap = pair.Key;
                        maxCount = pair.Value;
                    }

                    if (mostUsedMap == Localizer["extend_map"])
                    {
                        Server.PrintToChatAll("Extend");
                        ExtendMap();
                        return;
                    }
                }
                else
                {
                    mostUsedMap ??= _categoryMaps.Keys.ElementAt(new Random().Next(_categoryMaps.Count));
                }

                var rMaps = new List<string>();
                rMaps = SelectRandomMaps(_categoryMaps[mostUsedMap], _playedMap,
                    _nominateMap.TryGetValue(mostUsedMap, out var value) ? value : new List<string>());
                StartTimeSelectMaps(rMaps, forced);
            });
            foreach (var player in Utilities.GetPlayers())
            {
                ChatMenus.OpenMenu(player, menu);
            }
        }
        else
        {
            var rMaps = SelectRandomMaps(_mapsList, _playedMap, _nominateMap["None"]);
            StartTimeSelectMaps(rMaps, forced);
        }
    }

    private void ExtendMap()
    {
        _isExtendMap = true;
        if(_cTimeLimit > 0)
        {
            var time = ConVar.Find("mp_timelimit")!.GetPrimitiveValue<float>() + _config.ExtendTime;
            Server.PrintToChatAll(Localizer["select_extend_map", _config.ExtendTime]);
            Server.ExecuteCommand($"mp_timelimit {time}");
            _votedRtv = 0;
            _selectMapCount.Clear();
            _isStartVote = false;
            AddTimer(1.0f, (() =>
                _timeLimit = ConVar.Find("mp_timelimit")!.GetPrimitiveValue<float>() * 60.0f + Server.EngineTime));
        }
        else
        {
            _maxrounds += _config.ExtendRound;
            var round = ConVar.Find("mp_maxrounds")!.GetPrimitiveValue<int>() + _config.ExtendRound;
            Server.ExecuteCommand($"mp_maxrounds {round}");
        }

        foreach (var player in Utilities.GetPlayers())
            _users[player.Index].IsRtv = false;
    }

    private void StartTimeSelectMaps(List<string> maps, bool forced = false)
    {
        _selectMapCount.Clear();

        _menuSelectMap = new ChatMenu("MapChooser");
        if (_mapsList.Count != 0 && !_isExtendMap && forced)
            _menuSelectMap.AddMenuOption(Localizer["extend_map"], OnSelectCategory);
        foreach (var map in maps)
        {
            _menuSelectMap.AddMenuOption(map, (controller, option) =>
            {
                Server.PrintToChatAll(Localizer["vote_player", controller.PlayerName, option.Text]);
                _users[controller.Index].SelectMap = option.Text;
                if (!_selectMapCount.TryAdd(option.Text, 1))
                {
                    _selectMapCount[option.Text]++;
                }
            });
        }

        foreach (var player in Utilities.GetPlayers())
        {
            ChatMenus.OpenMenu(player, _menuSelectMap);
        }

        AddTimer(_config.TimeVote, () =>
        {
            var mostUsedMap = (string?)null;
            var maxCount = 0;
            _isStartVote = false;
            if (_selectMapCount.Count != 0)
            {
                foreach (KeyValuePair<string, int> pair in _selectMapCount)
                {
                    if (pair.Value <= maxCount) continue;

                    mostUsedMap = pair.Key;
                    maxCount = pair.Value;
                }

                if (mostUsedMap == Localizer["extend_map"])
                {
                    ExtendMap();
                    return;
                }
            }
            else
            {
                mostUsedMap ??= maps[new Random().Next(0, maps.Count)];
            }


            _nextmap = mostUsedMap;
            if (forced)
            {
                Server.PrintToChatAll(Localizer["end_vote_force", _nextmap]);
                AddTimer(2.0f, (() => ChangeMap(_nextmap)));
                return;
            }

            Server.PrintToChatAll(Localizer["nextmap_successfully", _nextmap]);
        });
    }

    private void ChangeMap(string map)
    {
        Server.ExecuteCommand(IsWorkshopMap(map) ? $"ds_workshop_changelevel {_nextmap}" : $"changelevel {_nextmap}");
    }

    private bool IsWorkshopMap(string mapName)
    {
        var configPath = Path.Combine(ModuleDirectory, "maps.json");
        var json = File.ReadAllText(configPath);
        var data = JObject.Parse(json);
        if(_mapsList.Count == 0)
        {
            var settings = (JObject)data["Settings"];

            if (settings.SelectMany<KeyValuePair<string, JToken>, JToken>(kvp =>
                    kvp.Value.SelectMany(jt => (jt["workshop"] as JArray))).Any(token => token.ToString() == mapName))
            {
                return true;
            }
        }
        else
        {
            var maps = (JArray)data["Maps"];
            return maps.Any(jt => (jt["workshop"] as JArray).Any(token => token.ToString() == mapName));
        }

        return false;
    }

    private List<string> SelectRandomMaps(List<string> allMaps, List<string> playedMaps, List<string> nominatedMaps)
    {
        var random = new Random();
        var availableMaps = allMaps.Except(playedMaps).ToList();
        if (availableMaps.Count == 1)
        {
            playedMaps.Clear();
        }

        var selectedMaps = new List<string>();
        foreach (var card in nominatedMaps)
        {
            if (!availableMaps.Contains(card)) continue;

            selectedMaps.Add(card);
            availableMaps.Remove(card);
            if (selectedMaps.Count == 7)
            {
                return selectedMaps;
            }
        }

        selectedMaps.AddRange(availableMaps.OrderBy(x => random.Next()).Take(7 - selectedMaps.Count));
        if (selectedMaps.Count < 7)
            selectedMaps.AddRange(allMaps.Except(playedMaps).Except(selectedMaps).OrderBy(x => random.Next())
                .Take(7 - selectedMaps.Count));

        return selectedMaps;
    }

    private void OnSelectCategory(CCSPlayerController player, ChatMenuOption option)
    {
        if (!_selectMapCount.TryAdd(option.Text, 1))
        {
            _selectMapCount[option.Text]++;
        }

        Server.PrintToChatAll(Localizer["select_category", player.PlayerName, option.Text]);
    }

    private Config LoadConfig()
    {
        var configPath = Path.Combine(ModuleDirectory, "settings.json");

        if (!File.Exists(configPath)) return CreateConfig(configPath);

        var config = JsonSerializer.Deserialize<Config>(File.ReadAllText(configPath))!;

        return config;
    }

    private Config CreateConfig(string configPath)
    {
        var config = new Config
        {
            Needed = 0.6,
            TimeVote = 20f,
            PlayedMaps = 5,
            StartVoteChangeMap = 5,
            ExtendTime = 10.0f,
            ExtendRound = 10,
            VotingRoundThreshold = 3
        };

        File.WriteAllText(configPath,
            JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true }));

        return config;
    }
}

public class UsersSettings
{
    public bool IsNominated { get; set; }
    public bool IsRtv { get; set; }
    public string? NominateMap { get; set; }
    public string? SelectMap { get; set; }
}

public class Config
{
    public int RtvTime { get; set; }
    public double Needed { get; set; }
    public float TimeVote { get; set; }
    public int PlayedMaps { get; set; }
    public float StartVoteChangeMap { get; set; }
    public int VotingRoundThreshold { get; set; }
    public float ExtendTime { get; set; }
    public int ExtendRound { get; set; }
}