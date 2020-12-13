﻿using CryptographyLabs.Crypto;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UnitTests
{
    [TestClass]
    public class RC4Test
    {
        [TestMethod]
        public void Test()
        {
            Random random = new Random(123);
            int byteSize = 800000;
            byte[] text = new byte[byteSize];
            random.NextBytes(text);
            for (int i = 0; i < 10; ++i)
            {
                byte[] key = new byte[8 * i + 8];
                random.NextBytes(key);

                var encrTrans = new RC4CryptoTransform(key);
                var decrTrans = new RC4CryptoTransform(key);

                byte[] encr = new byte[byteSize];
                encrTrans.TransformBlock(text, 0, byteSize, encr, 0);
                byte[] decr = new byte[byteSize];
                decrTrans.TransformBlock(encr, 0, byteSize, decr, 0);

                for (int j = 0; j < byteSize; ++j)
                    Assert.AreEqual(text[j], decr[j], $"{j}");
            }

        }

    }
}
