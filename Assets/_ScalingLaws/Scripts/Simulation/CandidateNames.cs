using ScalingLaws.Core;

namespace ScalingLaws.Simulation
{
    /// <summary>
    /// Names for the people who apply.
    ///
    /// **A candidate needs a name before they need anything else.** "Data Engineer, skill 3" is a
    /// row; "Priya Raghunathan, asking $184 an hour" is somebody the player either hires or does
    /// not. The list is deliberately international, because the labs this game is about hire from
    /// everywhere and a roster of one nationality would be a claim nobody made on purpose.
    ///
    /// Drawn from <see cref="DeterministicRandom"/> so a save reloads the same inbox.
    /// </summary>
    public static class CandidateNames
    {
        private static readonly string[] First =
        {
            "Amara", "Priya", "Yuki", "Lena", "Ines", "Nadia", "Sofia", "Mei", "Zara", "Elif",
            "Ada", "Rosa", "Hana", "Iris", "Nina", "Talia", "Freya", "Noor", "Clara", "Anya",
            "Kwame", "Diego", "Tomas", "Rahul", "Jonas", "Omar", "Kenji", "Levi", "Marek", "Idris",
            "Bruno", "Samir", "Otto", "Nikhil", "Ivan", "Cyrus", "Hugo", "Aleksy", "Tariq", "Emil",
            "Robin", "Alex", "Jamie", "Sacha", "Noa", "Kai", "Rowan", "Toni", "Jules", "Ari"
        };

        private static readonly string[] Last =
        {
            "Okonkwo", "Raghunathan", "Tanaka", "Vasquez", "Lindqvist", "Haddad", "Moreau", "Chen",
            "Novak", "Yilmaz", "Bergstrom", "Silva", "Nakamura", "Kovacs", "Petrov", "Adeyemi",
            "Duarte", "Farhadi", "Weiss", "Sorensen", "Iqbal", "Kaminski", "Rossi", "Mbeki",
            "Halvorsen", "Nasser", "Ferreira", "Wojcik", "Sandoval", "Aliyev", "Lindgren", "Osei",
            "Marchetti", "Bourdin", "Salgado", "Ivanova", "Reyes", "Dlamini", "Kirchner", "Aoki"
        };

        public static string Roll(DeterministicRandom random)
        {
            if (random == null)
            {
                return "Alex Chen";
            }

            var first = First[(int)(random.NextDouble() * First.Length) % First.Length];
            var last = Last[(int)(random.NextDouble() * Last.Length) % Last.Length];
            return $"{first} {last}";
        }
    }
}
