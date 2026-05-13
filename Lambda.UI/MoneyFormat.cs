using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lambda.UI
{
    public static class MoneyFormat
    {
        public static int Factor = 80;

        public static string FormatMoney(int number)
        {
            return FormatNumber(number * Factor);
        }

        static string FormatNumber(int number)
        {
            if (number >= 1000000000)
                return (number / 1000000000.0).ToString("0.#") + "B";
            if (number >= 1000000)
                return (number / 1000000.0).ToString("0.#") + "M";
            if (number >= 1000)
                return (number / 1000.0).ToString("0.#") + "K";

            return number.ToString();
        }
    }
}
