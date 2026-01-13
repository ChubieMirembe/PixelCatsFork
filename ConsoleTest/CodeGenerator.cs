using System;
using System.Security.Cryptography;

namespace ConsoleTest
{
    public static class CodeGenerator
    {
        public static string GenerateSixDigitCode()
        {
            Span<byte> bytes = stackalloc byte[4];
            RandomNumberGenerator.Fill(bytes);
            uint value = BitConverter.ToUInt32(bytes) % 1_000_000;
            return value.ToString("D6");
        }
    }
}