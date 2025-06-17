using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SP.Modules.Monthly.Models
{
    interface IMonthlyComment
    {
        string? Comment { get; set; }
        DateTime? MonthDate { get; set; }
    }
}
