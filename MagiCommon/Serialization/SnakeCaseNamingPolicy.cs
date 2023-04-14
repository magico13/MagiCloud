using System.Text.Json;

namespace MagiCommon.Serialization
{
    public class SnakeCaseNamingPolicy : JsonNamingPolicy
    {
        public override string ConvertName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return name;
            }

            var length = name.Length;
            var result = "";
            var isPreviousCapital = false;

            for (var i = 0; i < length; i++)
            {
                if (char.IsUpper(name[i]) && i > 0 && !isPreviousCapital)
                {
                    result += "_";
                }

                isPreviousCapital = char.IsUpper(name[i]);
                result += char.ToLowerInvariant(name[i]);
            }

            return result;
        }
    }
}
