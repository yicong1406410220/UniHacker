﻿
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace UniHacker
{
    internal class UnityPatcher : Patcher
    {
        UnityPatchInfo patchInfo;
        byte[] fileBytes;
        List<int> patchIndexes;

        public UnityPatcher(string filePath) : base(filePath)
        {
            PatchStatus = PatchStatus.NotSupport;

            patchInfo = UnityPatchInfos.FindPatchInfo(FileVersion, ArchitectureType);
            if (patchInfo?.IsValid() ?? false)
            {
                fileBytes = File.ReadAllBytes(FilePath);

                var darkIndexes = BoyerMooreSearcher.FindPattern(patchInfo.DarkPattern, fileBytes);
                var lightIndexes = BoyerMooreSearcher.FindPattern(patchInfo.LightPattern, fileBytes);

                if (darkIndexes.Count == patchInfo.DarkPattern.Count)
                {
                    patchIndexes = darkIndexes;
                    PatchStatus = PatchStatus.Patched;
                }
                else if (lightIndexes.Count == patchInfo.LightPattern.Count)
                {
                    patchIndexes = lightIndexes;
                    PatchStatus = PatchStatus.Support;
                }
            }
        }

        public override async Task<(bool success, string errorMsg)> ApplyPatch(Action<double> progress)
        {
            if (patchInfo != null && patchIndexes?.Count > 0)
            {
                // 创建备份
                var bakPath = FilePath + ".bak";
                if (File.Exists(bakPath))
                    File.Delete(bakPath);
                File.WriteAllBytes(bakPath, fileBytes);

                // 写入文件流
                for (var i = 0; i < patchInfo.DarkPattern.Count; i++)
                {
                    var bytes = patchInfo.DarkPattern[i];
                    for (int j = 0; j < bytes.Length; j++)
                    {
                        fileBytes[patchIndexes[i] + j] = bytes[j];
                    }
                }

                using (var sw = File.OpenWrite(FilePath))
                    sw.Write(fileBytes, 0, fileBytes.Length);

                // 2017以上版本特殊处理
                if (MajorVersion >= 2017)
                {
                    var licensingFilePath = Path.Combine(RootPath, @"Data\Resources\Licensing\Client\Unity.Licensing.Client.exe");
                    if (File.Exists(licensingFilePath))
                        File.Move(licensingFilePath, licensingFilePath + ".bak");
                }

                // 创建许可证
                LicensingInfo.TryGenerate(MajorVersion, MinorVersion);

                return (true, string.Empty);
            }

            return (false, string.Empty);
        }
    }
}