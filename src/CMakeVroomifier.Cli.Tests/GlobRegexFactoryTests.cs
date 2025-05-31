namespace CMakeVroomifier.Cli.Tests;

public class GlobRegexFactoryTests
{
    [Theory]
    // Matches extension, even in subfolder
    [InlineData("*.txt", "foo.txt", true)]
    [InlineData("*.txt", "src/foo.txt", true)]
    [InlineData("*.txt", "src\\foo.txt", true)]
    [InlineData("*.txt", "foo.cs", false)]
    [InlineData("*.txt", "src/foo.cs", false)]
    [InlineData("*.txt", "src\\foo.cs", false)]
    // Matches specific file name, even in subfolder
    [InlineData("file.txt", "file.txt", true)]
    [InlineData("file.txt", "src/file.txt", true)]
    [InlineData("file.txt", "src\\file.txt", true)]
    [InlineData("file.txt", "other.txt", false)]
    [InlineData("file.txt", "src/other.txt", false)]
    [InlineData("file.txt", "src\\other.txt", false)]
    // Matches extension in specific folder, but not subdirectories
    [InlineData("src/*.cs", "src/foo.cs", true)]
    [InlineData("src/*.cs", "src\\foo.cs", true)]
    [InlineData("src\\*.cs", "src/foo.cs", true)]
    [InlineData("src\\*.cs", "src\\foo.cs", true)]
    [InlineData("src/*.cs", "src/foo.txt", false)]
    [InlineData("src/*.cs", "src\\foo.txt", false)]
    [InlineData("src\\*.cs", "src/foo.txt", false)]
    [InlineData("src\\*.cs", "src\\foo.txt", false)]
    // Matches files in specific folder with any name and depth
    [InlineData("src/*", "src/foo.txt", true)]
    [InlineData("src/*", "src/pack/bar.cs", true)]
    [InlineData("src/*", "other/bar.cs", false)]
    // Matches files in specific folder with any name
    [InlineData("src/??.cs", "src/ab.cs", true)]
    [InlineData("src/??.cs", "src/a.cs", false)]
    [InlineData("src/??.cs", "src/abc.cs", false)]
    // Matches any file in any subdirectory
    [InlineData("src/", "src/foo.txt", true)]
    [InlineData("src/", "src/pack/foo.txt", true)]
    [InlineData("src/", "other/foo.txt", false)]
    [InlineData("src/", "foo.txt", false)]
    public void Should_create_regex_that_match_expected_paths(string pattern, string testPath, bool shouldMatch)
    {
        var regex = GlobRegexFactory.Create(pattern);

        regex.IsMatch(testPath.Replace('/', Path.DirectorySeparatorChar))
             .ShouldBe(shouldMatch);
    }
}
