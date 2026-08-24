using SyncTrayzor.Services.Config;
using SyncTrayzor.Services.Theming;
using Xunit;

namespace SyncTrayzor.Tests
{
    public class ConfigurationThemeTests
    {
        [Fact]
        public void DefaultTheme_FollowsSystem()
        {
            var configuration = new Configuration();

            Assert.Equal(ApplicationTheme.System, configuration.Theme);
        }

        [Theory]
        [InlineData(ApplicationTheme.System)]
        [InlineData(ApplicationTheme.Light)]
        [InlineData(ApplicationTheme.Dark)]
        public void CopyConstructor_PreservesTheme(ApplicationTheme theme)
        {
            var original = new Configuration { Theme = theme };

            var copy = new Configuration(original);

            Assert.Equal(theme, copy.Theme);
        }
    }
}
