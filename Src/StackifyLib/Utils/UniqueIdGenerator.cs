using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace StackifyLib.Utils
{
    public class UniqueIdGenerator
    {
        //FROM http://jopinblog.wordpress.com/2009/02/04/a-shorter-friendlier-guiduuid-in-net/

        private static readonly UniqueIdGenerator _instance = new UniqueIdGenerator();
        private static char[] _charMap = { // 0, 1, O, and I omitted intentionally giving 32 (2^5) symbols
        '2', '3', '4', '5', '6', '7', '8', '9', 
        'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'j', 'k', 'l', 'm', 'n', 'p', 'q', 'r', 's', 't', 'u', 'v', 'w', 'x', 'y', 'z'
    };

        public static UniqueIdGenerator GetInstance()
        {
            return _instance;
        }

        private RNGCryptoServiceProvider _provider = new RNGCryptoServiceProvider();

        private UniqueIdGenerator()
        {
        }

        private void GetNext(byte[] bytes)
        {
            _provider.GetBytes(bytes);
        }

        public string GetId(int numDigits)
        {
            return GetId(new byte[0], numDigits);
        }

        public string GetId(byte[] basis, int numDigits)
        {
            int byteCount = 16;
            var randBytes = new byte[byteCount - basis.Length];
            GetNext(randBytes);
            var bytes = new byte[byteCount];
            Array.Copy(basis, 0, bytes, byteCount - basis.Length, basis.Length);
            Array.Copy(randBytes, 0, bytes, 0, randBytes.Length);

            ulong lo = (((ulong)BitConverter.ToUInt32(bytes, 8)) << 32) | BitConverter.ToUInt32(bytes, 12); // BitConverter.ToUInt64(bytes, 8);
            ulong hi = (((ulong)BitConverter.ToUInt32(bytes, 0)) << 32) | BitConverter.ToUInt32(bytes, 4);  // BitConverter.ToUInt64(bytes, 0);
            ulong mask = 0x1F;

            var chars = new char[26];
            int charIdx = 25;

            ulong work = lo;
            for (int i = 0; i < 26; i++)
            {
                if (i == 12)
                {
                    work = ((hi & 0x01) << 4) & lo;
                }
                else if (i == 13)
                {
                    work = hi >> 1;
                }
                byte digit = (byte)(work & mask);
                chars[charIdx] = _charMap[digit];
                charIdx--;
                work = work >> 5;
            }

            var ret = new string(chars, 26 - numDigits, numDigits);
            return ret;
        }
    }
}
