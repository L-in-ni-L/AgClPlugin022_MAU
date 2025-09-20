using Exiled.API.Interfaces;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AgClPlugin022_MAU
{
    public class Config : IConfig
    {
        [Description("是否启用日活插件")]
        public bool IsEnabled { get; set; } = true;
        public bool Debug { get; set; } = false;

        [Description("数据保留月数")]
        public int DataRetentionMonths { get; set; } = 2;
    }
}
