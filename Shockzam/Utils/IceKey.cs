using System;
using System.Text;

namespace Shockzam
{
    public class IceKey
    {
        private static ulong[,] spBox;
        private static bool spBoxInitialised;

        private static readonly int[,] sMod =
        {
            {
                333,
                313,
                505,
                369
            },

            {
                379,
                375,
                319,
                391
            },

            {
                361,
                445,
                451,
                397
            },

            {
                397,
                425,
                395,
                505
            }
        };

        private static readonly int[,] sXor =
        {
            {
                131,
                133,
                155,
                205
            },

            {
                204,
                167,
                173,
                65
            },

            {
                75,
                46,
                212,
                51
            },

            {
                234,
                203,
                46,
                4
            }
        };

        private static readonly uint[] pBox =
        {
            1u,
            128u,
            1024u,
            8192u,
            524288u,
            2097152u,
            16777216u,
            1073741824u,
            8u,
            32u,
            256u,
            16384u,
            65536u,
            8388608u,
            67108864u,
            536870912u,
            4u,
            16u,
            512u,
            32768u,
            131072u,
            4194304u,
            134217728u,
            268435456u,
            2u,
            64u,
            2048u,
            4096u,
            262144u,
            1048576u,
            33554432u,
            2147483648u
        };

        private static readonly int[] keyrot =
        {
            0,
            1,
            2,
            3,
            2,
            1,
            3,
            0,
            1,
            3,
            2,
            0,
            3,
            1,
            0,
            2
        };

        private readonly int[,] keySchedule;

        private readonly int rounds;
        /* ATTENTION!
         * This class was decompiles by ILSpy, a .NET decompiler which is
         * quite bad in terms of names of stuff, so the original varibales
         * name were diffrent. Anyway, it was used due to "unsafe" errors.
         */

        private readonly int size;

        public IceKey(int level)
        {
            if (!spBoxInitialised)
            {
                spBoxInit();
                spBoxInitialised = true;
            }

            if (level < 1)
            {
                size = 1;
                rounds = 8;
            }
            else
            {
                size = level;
                rounds = level * 16;
            }

            keySchedule = new int[rounds, 3];
        }

        private int gf_mult(int a, int b, int m)
        {
            var num = 0;
            while (b != 0)
            {
                if ((b & 1) != 0) num ^= a;
                a <<= 1;
                b >>= 1;
                if (a >= 256) a ^= m;
            }

            return num;
        }

        private long gf_exp7(int b, int m)
        {
            if (b == 0) return 0L;
            var num = gf_mult(b, b, m);
            num = gf_mult(b, num, m);
            num = gf_mult(num, num, m);
            return gf_mult(b, num, m);
        }

        private long perm32(long x)
        {
            var num = 0L;
            var num2 = 0L;
            while (x != 0L)
            {
                if ((x & 1L) != 0L) num |= pBox[(int) (IntPtr) num2];
                num2 += 1L;
                x >>= 1;
            }

            return num;
        }

        private void spBoxInit()
        {
            spBox = new ulong[4, 1024];
            for (var i = 0; i < 1024; i++)
            {
                var num = (i >> 1) & 255;
                var num2 = (i & 1) | ((i & 512) >> 8);
                var x = gf_exp7(num ^ sXor[0, num2], sMod[0, num2]) << 24;
                spBox[0, i] = (ulong) perm32(x);
                x = gf_exp7(num ^ sXor[1, num2], sMod[1, num2]) << 16;
                spBox[1, i] = (ulong) perm32(x);
                x = gf_exp7(num ^ sXor[2, num2], sMod[2, num2]) << 8;
                spBox[2, i] = (ulong) perm32(x);
                x = gf_exp7(num ^ sXor[3, num2], sMod[3, num2]);
                spBox[3, i] = (ulong) perm32(x);
            }
        }

        private void scheduleBuild(int[] kb, int n, int krot_idx)
        {
            for (var i = 0; i < 8; i++)
            {
                var num = keyrot[krot_idx + i];
                for (var j = 0; j < 3; j++) keySchedule[n + i, j] = 0;
                for (var j = 0; j < 15; j++)
                {
                    var num2 = j % 3;
                    for (var k = 0; k < 4; k++)
                    {
                        var num3 = kb[(num + k) & 3];
                        var num4 = num3 & 1;
                        keySchedule[n + i, num2] = (keySchedule[n + i, num2] << 1) | num4;
                        kb[(num + k) & 3] = (num3 >> 1) | ((num4 ^ 1) << 15);
                    }
                }
            }
        }

        public void set(char[] key)
        {
            var array = new int[4];
            if (rounds == 8)
            {
                for (var i = 0; i < 4; i++) array[3 - i] = ((key[i * 2] & 'ÿ') << 8) | (key[i * 2 + 1] & 'ÿ');
                scheduleBuild(array, 0, 0);
                return;
            }

            for (var i = 0; i < size; i++)
            {
                for (var j = 0; j < 4; j++) array[3 - j] = (key[i * 8 + j * 2] << 8) | key[i * 8 + j * 2 + 1];
                scheduleBuild(array, i * 8, 0);
                scheduleBuild(array, rounds - 8 - i * 8, 8);
            }
        }

        public void clear()
        {
            for (var i = 0; i < rounds; i++)
            for (var j = 0; j < 3; j++)
                keySchedule[i, j] = 0;
        }

        private ulong roundFunc(ulong p, int i, int[,] subkey)
        {
            var num = ((p >> 16) & 1023uL) | (((p >> 14) | (p << 18)) & 1047552uL);
            var num2 = (p & 1023uL) | ((p << 2) & 1047552uL);
            var num3 = (ulong) (subkey[i, 2] & (long) (num ^ num2));
            var num4 = num3 ^ num2;
            num3 ^= num;
            num3 ^= (ulong) subkey[i, 0];
            num4 ^= (ulong) subkey[i, 1];
            return spBox[(int) (IntPtr) 0L, (int) (IntPtr) (num3 >> 10)] |
                   spBox[(int) (IntPtr) 1L, (int) (IntPtr) (num3 & 1023uL)] |
                   spBox[(int) (IntPtr) 2L, (int) (IntPtr) (num4 >> 10)] |
                   spBox[(int) (IntPtr) 3L, (int) (IntPtr) (num4 & 1023uL)];
        }

        private void encrypt(byte[] plaintext, byte[] ciphertext, int idx)
        {
            var num = ((ulong) plaintext[idx] << 24) | ((ulong) plaintext[idx + 1] << 16) |
                      ((ulong) plaintext[idx + 2] << 8) | plaintext[idx + 3];
            var num2 = ((ulong) plaintext[idx + 4] << 24) | ((ulong) plaintext[idx + 5] << 16) |
                       ((ulong) plaintext[idx + 6] << 8) | plaintext[idx + 7];
            for (var i = 0; i < rounds; i += 2)
            {
                num ^= roundFunc(num2, i, keySchedule);
                num2 ^= roundFunc(num, i + 1, keySchedule);
            }

            for (var i = 0; i < 4; i++)
            {
                ciphertext[idx + 3 - i] = (byte) (num2 & 255uL);
                ciphertext[idx + 7 - i] = (byte) (num & 255uL);
                num2 >>= 8;
                num >>= 8;
            }
        }

        private void decrypt(byte[] ciphertext, byte[] plaintext)
        {
            var num = ((ulong) ciphertext[0] << 24) | ((ulong) ciphertext[1] << 16) | ((ulong) ciphertext[2] << 8) |
                      ciphertext[3];
            var num2 = ((ulong) ciphertext[4] << 24) | ((ulong) ciphertext[5] << 16) | ((ulong) ciphertext[6] << 8) |
                       ciphertext[7];
            for (var i = rounds - 1; i > 0; i -= 2)
            {
                num ^= roundFunc(num2, i, keySchedule);
                num2 ^= roundFunc(num, i - 1, keySchedule);
            }

            for (var i = 0; i < 4; i++)
            {
                plaintext[3 - i] = (byte) (num2 & 255uL);
                plaintext[7 - i] = (byte) (num & 255uL);
                num2 >>= 8;
                num >>= 8;
            }
        }

        public int keySize()
        {
            return size * 8;
        }

        public int blockSize()
        {
            return 8;
        }

        public char[] encString(string str)
        {
            var array = str.ToCharArray();
            var length = str.Length;
            var num = (length / 8 + 1) * 8;
            var array2 = new byte[num];
            var array3 = new byte[num];
            for (var i = 0; i < num; i++) array2[i] = 0;
            for (var j = 0; j < length; j++) array2[j] = (byte) array[j];
            for (var k = 0; k < num; k += 8) encrypt(array2, array3, k);
            var text = "#0x";
            for (var l = 0; l < num; l++)
            {
                var num2 = (int) array3[l];
                text += string.Format("{0:x2}", new object[]
                {
                    Convert.ToUInt32(num2.ToString())
                });
            }

            return text.ToCharArray();
        }

        public byte[] encBinary(byte[] data, int data_size)
        {
            var num = (data_size / 8 + 1) * 8;
            var array = new byte[num];
            var array2 = new byte[num];
            for (var i = 0; i < data_size; i++) array[i] = data[i];
            for (var j = 0; j < num; j += 8) encrypt(array, array2, j);
            return array2;
        }

        public string decString(string str)
        {
            str = str.Substring("#0x".Length);
            var stringBuilder = new StringBuilder();
            var array = str.ToCharArray();
            var num = array.Length;
            for (var i = 0; i < num; i += 16)
            {
                var array2 = new byte[8];
                var array3 = new byte[8];
                for (var j = 0; j < 8; j++)
                    array2[j] = Convert.ToByte(string.Concat(array[i + j * 2], array[i + j * 2 + 1]), 16);
                decrypt(array2, array3);
                for (var k = 0; k < 8; k++)
                    if (array3[k] != 0)
                        stringBuilder.Append(Convert.ToChar(array3[k]));
            }

            return stringBuilder.ToString();
        }
    }
}