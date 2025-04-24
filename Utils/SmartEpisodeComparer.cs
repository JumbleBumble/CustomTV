using System.Text.RegularExpressions;

namespace CustomTV.Utils
{
    public class SmartEpisodeComparer : IComparer<string>
    {
        private static readonly Regex seasonEpisodeRegex =
            new(@"S(\d+)[Eex](\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex leadingNumberRegex =
            new(@"^(\d+)", RegexOptions.Compiled);
        private static readonly Regex splitRegex =
            new(@"\d+|\D+", RegexOptions.Compiled);

        public int Compare(string x, string y)
        {
            string fnX = Path.GetFileName(x);
            string fnY = Path.GetFileName(y);

            var (seasonA, episodeA, numberA) = ExtractSortKey(fnX);
            var (seasonB, episodeB, numberB) = ExtractSortKey(fnY);

            if (seasonA.HasValue && seasonB.HasValue)
            {
                int c = seasonA.Value.CompareTo(seasonB.Value);
                if (c != 0) return c;
                return episodeA.Value.CompareTo(episodeB.Value);
            }

            if (seasonA.HasValue) return -1;
            if (seasonB.HasValue) return +1;

            if (numberA.HasValue && numberB.HasValue)
                return numberA.Value.CompareTo(numberB.Value);

            if (numberA.HasValue) return -1;
            if (numberB.HasValue) return +1;

            return NaturalCompare(fnX, fnY);
        }

        private static (int? Season, int? Episode, int? Number) ExtractSortKey(string filename)
        {
            var m = seasonEpisodeRegex.Match(filename);
            if (m.Success
                && int.TryParse(m.Groups[1].Value, out int season)
                && int.TryParse(m.Groups[2].Value, out int episode))
                return (season, episode, null);

            var n = leadingNumberRegex.Match(filename);
            if (n.Success && int.TryParse(n.Groups[1].Value, out int num))
                return (null, null, num);

            return (null, null, null);
        }

        private static int NaturalCompare(string a, string b)
        {
            var partsA = splitRegex.Matches(a);
            var partsB = splitRegex.Matches(b);
            int count = Math.Min(partsA.Count, partsB.Count);

            for (int i = 0; i < count; i++)
            {
                string pa = partsA[i].Value;
                string pb = partsB[i].Value;

                bool isNumA = char.IsDigit(pa[0]);
                bool isNumB = char.IsDigit(pb[0]);

                if (isNumA && isNumB)
                {
                    if (pa.Length != pb.Length)
                        return pa.Length.CompareTo(pb.Length);
                    int cmp = string.CompareOrdinal(pa, pb);
                    if (cmp != 0) return cmp;
                }
                else
                {
                    int cmp = string.Compare(pa, pb, StringComparison.OrdinalIgnoreCase);
                    if (cmp != 0) return cmp;
                }
            }

            return partsA.Count.CompareTo(partsB.Count);
        }
    }
}
