using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Globalization;

namespace HermesPOS.Helpers
{
    public static class DecimalHelper
    {
        public static bool TryParseFlexibleDecimal(string input, out decimal result)
        {
            result = 0;

            if (string.IsNullOrWhiteSpace(input))
                return false;

            var normalized = input.Trim().Replace(',', '.');

            return decimal.TryParse(
                normalized,
                NumberStyles.Any,
                CultureInfo.InvariantCulture,
                out result);
        }
    }
}
