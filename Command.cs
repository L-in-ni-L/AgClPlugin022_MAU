using CommandSystem;
using Exiled.API.Features;
using System;
using System.Linq;
using System.Text;

namespace AgClPlugin022_MAU
{
    [CommandHandler(typeof(ClientCommandHandler))]
    public class PlayerActivityCommand : ICommand
    {
        public string Command => "playeractivity";
        public string[] Aliases => new[] { "pa", "pactivity" };
        public string Description => "查看玩家活跃天数统计";

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            // 获取插件实例
            var plugin = PlayerActivityPlugin.Instance;
            if (plugin == null)
            {
                response = "插件未加载";
                return false;
            }

            if (arguments.Count == 0)
            {
                // 显示当前服务器所有玩家活跃天数排名
                var activityData = plugin.GetAllPlayersMonthlyActivity()
                    .OrderByDescending(kv => kv.Value)
                    .Take(20)
                    .ToList();

                var sb = new StringBuilder();
                sb.AppendLine("=== 本月活跃天数排行榜 ===");
                sb.AppendLine("排名 | 玩家 | 活跃天数");
                sb.AppendLine("----------------------");

                for (int i = 0; i < activityData.Count; i++)
                {
                    var kv = activityData[i];
                    string userId = kv.Key;
                    int days = kv.Value;
                    var record = plugin.GetPlayerRecord(userId);
                    sb.AppendLine($"{i + 1}. {record?.LastKnownName ?? "Unknown"} | {days}天");
                }

                response = sb.ToString();
                return true;
            }

            // 查询特定玩家
            string search = arguments.At(0);
            var player = Player.Get(search) ?? Player.List.FirstOrDefault(p =>
                p.Nickname.Equals(search, StringComparison.OrdinalIgnoreCase) ||
                p.UserId.Contains(search));

            if (player == null)
            {
                response = $"找不到玩家: {search}";
                return false;
            }

            var playerRecord = plugin.GetPlayerRecord(player.UserId);
            if (playerRecord == null)
            {
                response = $"{player.Nickname} 本月尚未活跃";
                return true;
            }

            // 获取最近活跃日期
            var recentDates = playerRecord.ActiveDates
                .OrderByDescending(d => d)
                .Take(5)
                .ToList();

            response = $"[{player.UserId}]\n" +
                       $"昵称: {playerRecord.LastKnownName}\n" +
                       $"本月活跃: {playerRecord.CurrentMonthActiveDays}天\n" +
                       $"总活跃: {playerRecord.TotalActiveDays}天\n" +
                       $"最近活跃: {string.Join(", ", recentDates)}";

            return true;
        }
    }

}
