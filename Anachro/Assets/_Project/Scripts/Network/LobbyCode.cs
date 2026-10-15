namespace Anachronism.Network
{
    /// <summary>
    /// Code de partie à 7 caractères tiré de l'identifiant du lobby Steam. Fonctions pures : pas d'état, testables.
    /// Un lobby Steam a toujours les mêmes 32 bits hauts (univers public, compte « chat », drapeaux lobby) :
    /// seul le numéro de compte (32 bits bas) varie, ce qui tient en 7 caractères base 32.
    /// </summary>
    public static class LobbyCode
    {
        public const int Length = 7;

        // Base 32 de Crockford : pas de I, L, O, U, pour éviter les confusions à la lecture et à l'oral.
        private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";
        private const int BitsPerChar = 5;
        private const ulong LobbyIdHighBits = 0x0186000000000000UL;
        private const ulong HighBitsMask = 0xFFFFFFFF00000000UL;

        /// <summary>Faux si l'identifiant n'a pas la forme d'un lobby Steam (le code ne permettrait pas de le retrouver).</summary>
        public static bool TryEncode(ulong lobbyId, out string code)
        {
            code = string.Empty;
            if ((lobbyId & HighBitsMask) != LobbyIdHighBits)
                return false;

            uint accountId = (uint)lobbyId;
            char[] characters = new char[Length];
            for (int i = Length - 1; i >= 0; i--)
            {
                characters[i] = Alphabet[(int)(accountId & 31u)];
                accountId >>= BitsPerChar;
            }

            code = new string(characters);
            return true;
        }

        /// <summary>Tolère minuscules, espaces, tirets et les confusions O/0, I/1, L/1.</summary>
        public static bool TryDecode(string code, out ulong lobbyId)
        {
            lobbyId = 0;
            if (string.IsNullOrWhiteSpace(code))
                return false;

            ulong accountId = 0;
            int count = 0;
            foreach (char rawCharacter in code.ToUpperInvariant())
            {
                if (rawCharacter == ' ' || rawCharacter == '-')
                    continue;

                char character = rawCharacter switch
                {
                    'O' => '0',
                    'I' => '1',
                    'L' => '1',
                    _ => rawCharacter
                };

                int value = Alphabet.IndexOf(character);
                if (value < 0)
                    return false;

                accountId = (accountId << BitsPerChar) | (uint)value;
                count++;
            }

            // 7 caractères = 35 bits : au-delà de 32 bits, ce n'est pas un code que TryEncode a pu produire.
            if (count != Length || accountId > uint.MaxValue)
                return false;

            lobbyId = LobbyIdHighBits | accountId;
            return true;
        }

        /// <summary>Affichage lisible : « K7Q2-M4X ».</summary>
        public static string Format(string code) =>
            code.Length == Length ? $"{code.Substring(0, 4)}-{code.Substring(4)}" : code;
    }
}
