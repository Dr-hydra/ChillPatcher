namespace MeloongCore.Tests;
public class DirectoryUtilsTest : TestWithFolder {

    #region 剪切、复制

    [Test]
    [Arguments("Source", "目标文件夹")]
    [Arguments("Source", "Source")]
    [Arguments("Source", "soURCE")]
    public async Task 复制(string sourceName, string destName) {
        var src = Path.Combine(tempFolder, sourceName);
        DirectoryUtils.Create(Path.Combine(src, "sub"));
        FileUtils.Write(Path.Combine(src, "a.txt"), "aaa");
        FileUtils.Write(Path.Combine(src, "sub", "b file.txt"), "bbb");
        var dest = Path.Combine(tempFolder, destName);

        DirectoryUtils.Copy(src, dest);

        await Assert.That(FileUtils.ReadAsString(Path.Combine(src, "a.txt"))).IsEqualTo("aaa");
        await Assert.That(FileUtils.ReadAsString(Path.Combine(dest, "a.txt"))).IsEqualTo("aaa");
        await Assert.That(FileUtils.ReadAsString(Path.Combine(src, "sub", "b file.txt"))).IsEqualTo("bbb");
        await Assert.That(FileUtils.ReadAsString(Path.Combine(dest, "sub", "b file.txt"))).IsEqualTo("bbb");
        await Assert.That(DirectoryUtils.GetDirectories(Path.Combine(tempFolder))).Contains(dest); // 目标文件夹名
    }

    [Test]
    [Arguments("Source", "目标文件夹")]
    [Arguments("Source", "Source")]
    [Arguments("Source", "soURCE")]
    public async Task 剪切(string sourceName, string destName) {
        var src = Path.Combine(tempFolder, sourceName);
        DirectoryUtils.Create(Path.Combine(src, "sub"));
        FileUtils.Write(Path.Combine(src, "a.txt"), "aaa");
        FileUtils.Write(Path.Combine(src, "sub", "b file.txt"), "bbb");
        var dest = Path.Combine(tempFolder, destName);

        DirectoryUtils.Move(src, dest);

        await Assert.That(FileUtils.ReadAsString(Path.Combine(dest, "a.txt"))).IsEqualTo("aaa");
        await Assert.That(FileUtils.ReadAsString(Path.Combine(dest, "sub", "b file.txt"))).IsEqualTo("bbb");
        if (src != dest) await Assert.That(DirectoryUtils.GetDirectories(Path.Combine(tempFolder))).DoesNotContain(src);
        await Assert.That(DirectoryUtils.GetDirectories(Path.Combine(tempFolder))).Contains(dest); // 目标文件夹名
    }

    [Test]
    public async Task 剪切_不同磁盘() {
        // 寻找两个磁盘
        var drives = DriveInfo.GetDrives()
            .Where(d => d.IsReady && d.DriveType == DriveType.Fixed)
            .ToArray();
        if (drives.Length < 2) return;

        var id = Guid.NewGuid().ToString("N");
        var srcRoot = Path.Combine(drives[0].RootDirectory.FullName, "Temp", $"MeloongTest_{id}");
        var destRoot = Path.Combine(drives[1].RootDirectory.FullName, "Temp", $"MeloongTest_{id}");
        var src = Path.Combine(srcRoot, "src");
        var dest = Path.Combine(destRoot, "dest");
        try {
            DirectoryUtils.Create(src);
            FileUtils.Write(Path.Combine(src, "a.txt"), "aaa");
            FileUtils.Write(Path.Combine(src, "sub", "b file.txt"), "bbb");

            DirectoryUtils.Move(src, dest);

            await Assert.That(DirectoryUtils.Exists(src)).IsFalse();
            await Assert.That(FileUtils.ReadAsString(Path.Combine(dest, "a.txt"))).IsEqualTo("aaa");
            await Assert.That(FileUtils.ReadAsString(Path.Combine(dest, "sub", "b file.txt"))).IsEqualTo("bbb");
        } finally {
            DirectoryUtils.Delete(srcRoot);
            DirectoryUtils.Delete(destRoot);
        }
    }

    [Test]
    public void 剪切_安全检查() 
        => Assert.Throws<UnauthorizedAccessException>(() => DirectoryUtils.Move(PathUtils.CurrentFolder, Path.Combine(tempFolder, "SafetyCheckDest")));

    #endregion

}
