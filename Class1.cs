using Exiled.API.Features;
using Exiled.API.Interfaces;
using Exiled.Events.EventArgs.Player;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;

namespace AgClPlugin022_MAU
{
    public class PlayerActivityPlugin : Plugin<Config>
    {
        public static PlayerActivityPlugin Instance { get; private set; }

        public override string Name => "Player Monthly Activity Tracker";
        public override string Author => "YourName";
        public override Version Version => new Version(1, 3, 0);
        public override Version RequiredExiledVersion => new Version(5, 2, 2);

        private static readonly string DataPath = Path.Combine(Paths.Plugins, "player_activity_data.json");
        private PlayerActivityData _activityData;
        private DateTime _lastCheckDate;

        public PlayerActivityPlugin()
        {
            Instance = this; // 初始化单例
        }

        public override void OnEnabled()
        {
            LoadData();
            _lastCheckDate = DateTime.UtcNow.Date;

            Exiled.Events.Handlers.Player.Verified += OnPlayerVerified;
            Exiled.Events.Handlers.Server.WaitingForPlayers += OnRoundRestart;

            Log.Info($"Player activity tracker enabled! Loaded {_activityData.Players.Count} players");
        }

        public override void OnDisabled()
        {
            Exiled.Events.Handlers.Player.Verified -= OnPlayerVerified;
            Exiled.Events.Handlers.Server.WaitingForPlayers -= OnRoundRestart;
            SaveData();
            Log.Info("Player activity tracker disabled!");
        }

        private void OnRoundRestart()
        {
            // 每天UTC午夜自动清理旧数据
            if (DateTime.UtcNow.Date > _lastCheckDate)
            {
                CleanOldData();
                _lastCheckDate = DateTime.UtcNow.Date;
            }
        }

        private void OnPlayerVerified(VerifiedEventArgs ev)
        {
            DateTime today = DateTime.UtcNow.Date;
            string userId = ev.Player.UserId;
            string dateKey = today.ToString("yyyy-MM-dd");

            // 获取或创建玩家记录
            if (!_activityData.Players.TryGetValue(userId, out var playerRecord))
            {
                playerRecord = new PlayerActivityRecord
                {
                    UserId = userId,
                    LastKnownName = ev.Player.Nickname
                };
                _activityData.Players[userId] = playerRecord;
            }
            else
            {
                // 更新玩家名称
                playerRecord.LastKnownName = ev.Player.Nickname;
            }

            // 添加今日活跃记录（如果尚未记录）
            if (playerRecord.ActiveDates.Add(dateKey))
            {
                // 更新本月活跃天数
                playerRecord.CurrentMonthActiveDays = CalculateCurrentMonthDays(playerRecord);
                SaveData();
            }
        }

        private int CalculateCurrentMonthDays(PlayerActivityRecord player)
        {
            string currentMonthPrefix = DateTime.UtcNow.ToString("yyyy-MM");
            return player.ActiveDates.Count(d => d.StartsWith(currentMonthPrefix));
        }

        private void CleanOldData()
        {
            DateTime cutoffDate = DateTime.UtcNow.AddMonths(-Config.DataRetentionMonths);
            string cutoffKey = cutoffDate.ToString("yyyy-MM-dd");

            int removedCount = 0;
            int removedPlayers = 0;

            // 创建副本避免修改集合时迭代
            var playerIds = _activityData.Players.Keys.ToList();

            foreach (var userId in playerIds)
            {
                var playerRecord = _activityData.Players[userId];

                // 移除旧日期记录
                int beforeCount = playerRecord.ActiveDates.Count;
                playerRecord.ActiveDates.RemoveWhere(d => string.Compare(d, cutoffKey) < 0);
                removedCount += (beforeCount - playerRecord.ActiveDates.Count);

                // 重新计算本月活跃天数
                playerRecord.CurrentMonthActiveDays = CalculateCurrentMonthDays(playerRecord);

                // 如果玩家没有活跃记录了，移除
                if (playerRecord.ActiveDates.Count == 0)
                {
                    _activityData.Players.Remove(userId);
                    removedPlayers++;
                }
            }

            Log.Info($"Cleaned data: Removed {removedCount} old dates and {removedPlayers} inactive players");
            SaveData();
        }

        public PlayerActivityRecord GetPlayerRecord(string userId)
        {
            return _activityData.Players.TryGetValue(userId, out var record) ? record : null;
        }

        public Dictionary<string, int> GetAllPlayersMonthlyActivity()
        {
            return _activityData.Players.ToDictionary(
                kv => kv.Key,
                kv => kv.Value.CurrentMonthActiveDays
            );
        }

        private void LoadData()
        {
            if (File.Exists(DataPath))
            {
                try
                {
                    string json = File.ReadAllText(DataPath);
                    _activityData = JsonConvert.DeserializeObject<PlayerActivityData>(json) ?? new PlayerActivityData();
                    Log.Info($"Loaded activity data for {_activityData.Players.Count} players");
                }
                catch (Exception ex)
                {
                    Log.Error($"Failed to load data: {ex}");
                    _activityData = new PlayerActivityData();
                }
            }
            else
            {
                _activityData = new PlayerActivityData();
            }
        }

        private void SaveData()
        {
            try
            {
                string json = JsonConvert.SerializeObject(_activityData, Formatting.Indented);
                File.WriteAllText(DataPath, json);
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to save data: {ex}");
            }
        }

        // ================= 数据类 =================
        public class PlayerActivityData
        {
            public Dictionary<string, PlayerActivityRecord> Players { get; set; } = new Dictionary<string, PlayerActivityRecord>();
        }

        public class PlayerActivityRecord
        {
            public string UserId { get; set; }
            public string LastKnownName { get; set; }

            [JsonProperty]
            public HashSet<string> ActiveDates { get; set; } = new HashSet<string>();

            public int CurrentMonthActiveDays { get; set; }

            [JsonIgnore]
            public int TotalActiveDays => ActiveDates.Count;
        }
    }
}
