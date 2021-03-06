﻿using RSAEncryption.Encryption;
using System;
using System.Collections.Generic;
using System.IO;

namespace RSAEncryption.Tests
{
    public static class Setup
    {
        public readonly static string AbsolutePath = Environment.CurrentDirectory;
        public readonly static string OriginalPath = Path.Combine(Environment.CurrentDirectory, "original");
        public readonly static string EncryptedPath = Path.Combine(Environment.CurrentDirectory, "encrypted");
        public readonly static string DecryptedPath = Path.Combine(Environment.CurrentDirectory, "decrypted");

        private static EncryptionKeyPair _pubkey;
        private static EncryptionKeyPair _privkey;
        public static EncryptionKeyPair PublicKey
        {
            get { return _pubkey; }
            set
            {
                if (_pubkey == null)
                    _pubkey = value;
            }
        }
        public static EncryptionKeyPair PrivateKey
        {
            get { return _privkey; }
            set
            {
                if (_privkey == null)
                    _privkey = value;
            }
        }

        public static void Initialize(out Dictionary<string, string> testFolders)
        {
            Guid[] guids = {
                Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()
            };
            string folderName = Guid.NewGuid().ToString();
            testFolders = new Dictionary<string, string>
            {
                { "original", Path.Combine(OriginalPath,folderName) },
                { "encrypted", Path.Combine(EncryptedPath,folderName) },
                { "decrypted", Path.Combine(DecryptedPath,folderName) },
            };

            string[] files = {
                Path.Combine(testFolders["original"], guids[0].ToString() + ".txt"),
                Path.Combine(testFolders["original"], guids[1].ToString() + ".txt"),
                Path.Combine(testFolders["original"], guids[2].ToString() + ".txt"),
            };

            string pubKey = @$"{AbsolutePath}\pub.key.pem";
            string privKey = @$"{AbsolutePath}\priv.key.pem";

            Directory.CreateDirectory(testFolders["original"]);
            Directory.CreateDirectory(testFolders["encrypted"]);
            Directory.CreateDirectory(testFolders["decrypted"]);

            for (int i = 0; i < files.Length; i++)
            {
                using (var sw = File.CreateText(files[i]))
                {
                    sw.WriteLine(Guid.NewGuid());
                    sw.WriteLine(Guid.NewGuid());
                    sw.WriteLine(Guid.NewGuid());
                }
            }

            if (!File.Exists(pubKey) && !File.Exists(privKey))
            {
                var key = EncryptionKeyPair.New(2048);
                key.ExportAsPEMFile(AbsolutePath, includePrivate: false);
                key.ExportAsPEMFile(AbsolutePath, includePrivate: true);
            }

            PrivateKey = EncryptionKeyPair.ImportPEMFile(@$"{AbsolutePath}\priv.key.pem");
            PublicKey = EncryptionKeyPair.ImportPEMFile(@$"{AbsolutePath}\pub.key.pem");
        }

        public static void SetEncryptedFiles(Dictionary<string, string> testFolders, bool multiple = false)
        {
            int files = Directory.GetFiles(testFolders["original"]).Length;

            int i = 0;
            do
            {
                string filePath = Directory.GetFiles(testFolders["original"])[i];

                FileManipulation.OpenFile(filePath, out var originalFile);

                string encryptedFileName = Path.GetFileNameWithoutExtension(filePath) + ".encrypted.txt";
                string encryptedPathFile = Path.Combine(testFolders["encrypted"], encryptedFileName);

                byte[] encryptedFile = PrivateKey.EncryptRijndael(originalFile);
                File.WriteAllBytes(encryptedPathFile, encryptedFile);
                i++;
            } while (i < (multiple ? files : 1));
        }

        public static void SetSignatureFile(Dictionary<string, string> testFolders, string hashalg = "SHA256")
        {
            SetEncryptedFiles(testFolders);

            var filePath = Directory.GetFiles(testFolders["original"])[0];
            var fileName = Path.GetFileNameWithoutExtension(filePath);
            var fileExt = Path.GetExtension(filePath);
            FileManipulation.OpenFile(filePath, out var originalFile);

            string output = Path.Combine(testFolders["encrypted"], $"{fileName}.{hashalg}{fileExt}");
            var signatureData = PrivateKey.SignData(originalFile, hashalg);
            File.WriteAllBytes(output, signatureData);
        }

        public static void SetMergedFile(Dictionary<string, string> testFolders, string hashalg)
        {
            SetSignatureFile(testFolders, hashalg);

            string output = testFolders["encrypted"];
            string originalFilePath = Directory.GetFiles(testFolders["original"])[0];
            string fileName = Path.GetFileNameWithoutExtension(originalFilePath);
            string signatureFilePath = Directory.GetFiles(testFolders["encrypted"], $"*{fileName}.SHA256*")[0];

            FileManipulation.OpenFile(originalFilePath, out var data);
            FileManipulation.OpenFile(signatureFilePath, out var signature);

            byte[] mergedFile = new byte[signature.Length + data.Length];
            using (var ms = new MemoryStream(mergedFile))
            {
                ms.Write(signature, 0, signature.Length);
                ms.Write(data, 0, data.Length);
            }

            File.WriteAllBytes($"{output}\\{fileName}.merged.txt", mergedFile);
        }
    }
}
