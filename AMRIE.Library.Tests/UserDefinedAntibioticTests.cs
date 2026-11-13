using AMRIE.Library;
using Xunit;

namespace AMRIE.Library.Tests
{
    public class UserDefinedAntibioticTests
    {
        [Theory]
        [InlineData("PEN_ND10")]
        [InlineData("AMK_ED30")]
        [InlineData("AMP_EM")]
        [InlineData("FOX_FE")]
        [InlineData("CRO_NM")]
        [InlineData("VAN_NE")]
        [InlineData("X_1_ND10")]
        [InlineData("X_2_EM")]
        [InlineData("X_99_FE")]
        [InlineData("X_10_NM")]
        [InlineData("X_5_ED25")]
        public void ValidAntibioticFieldNameRegex_MatchesValidFieldNames(string fieldName)
        {
            Assert.True(IsolateInterpretation.ValidAntibioticFieldNameRegex.IsMatch(fieldName),
                $"Expected '{fieldName}' to match ValidAntibioticFieldNameRegex.");
        }

        [Theory]
        [InlineData("X__ND10")]       // No digit (must be X_# or X_##)
        [InlineData("X_0_ND10")]      // Single digit zero disallowed
        [InlineData("X_00_ND10")]     // Leading zero (X_0# disallowed)
        [InlineData("X_01_ND10")]     // Leading zero (X_0# disallowed)
        [InlineData("X_05_ED25")]     // Leading zero (X_0# disallowed)
        [InlineData("X_100_ND10")]    // Three digits (must be X_# or X_##)
        [InlineData("X_AA_ND10")]     // Non-numeric
        [InlineData("X_ND10")]        // Missing number
        [InlineData("X_1")]           // Missing guideline and method
        [InlineData("X_13")]          // Missing guideline and method
        [InlineData("PEN_NC")]        // Invalid test method 'C'
        [InlineData("PEN_NF")]        // Invalid test method 'F'
        [InlineData("PEN_")]          // Trailing underscore only
        [InlineData("P_ND10")]        // Short antibiotic code
        public void ValidAntibioticFieldNameRegex_RejectsInvalidFieldNames(string fieldName)
        {
            Assert.False(IsolateInterpretation.ValidAntibioticFieldNameRegex.IsMatch(fieldName),
                $"Expected '{fieldName}' to be rejected by ValidAntibioticFieldNameRegex.");
        }

        [Theory]
        [InlineData("PEN")]
        [InlineData("AMK")]
        [InlineData("AMP")]
        [InlineData("CRO")]
        [InlineData("X_1")]
        [InlineData("X_2")]
        [InlineData("X_10")]
        [InlineData("X_99")]
        public void ValidAntibioticCode_MatchesValidCodes(string code)
        {
            Assert.True(IsolateInterpretation.ValidAntibioticCode.IsMatch(code),
                $"Expected '{code}' to match ValidAntibioticCode.");
        }

        [Theory]
        [InlineData("X_0")]
        [InlineData("X_00")]
        [InlineData("X_01")]
        [InlineData("X_09")]
        [InlineData("X_100")]
        [InlineData("X_AA")]
        [InlineData("PE")]
        [InlineData("PENN")]
        public void ValidAntibioticCode_RejectsInvalidCodes(string code)
        {
            Assert.False(IsolateInterpretation.ValidAntibioticCode.IsMatch(code),
                $"Expected '{code}' to be rejected by ValidAntibioticCode.");
        }

        [Theory]
        [InlineData("X_1_ND10", "X_1")]
        [InlineData("X_2_EM", "X_2")]
        [InlineData("X_10_NM", "X_10")]
        [InlineData("X_99_FE", "X_99")]
        [InlineData("X_1", "X_1")]
        [InlineData("X_10", "X_10")]
        [InlineData("X_99", "X_99")]
        [InlineData("PEN_ND10", "PEN")]
        [InlineData("AMK_ED30", "AMK")]
        [InlineData("CRO_NM", "CRO")]
        [InlineData("PEN", "PEN")]
        public void ShortCode_ReturnsExpectedAntibioticCode(string input, string expected)
        {
            string actual = Antibiotic.ShortCode(input);
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void AntibioticComponents_ParsesStandardAntibiotic()
        {
            var components = new AntibioticComponents("PEN_ND10");
            Assert.Equal("PEN", components.Code);
            Assert.Equal(Antibiotic.GuidelineNames.CLSI, components.Guideline);
            Assert.Equal(Antibiotic.TestMethods.Disk, components.TestMethod);
        }

        [Fact]
        public void AntibioticComponents_ParsesUserDefinedAntibiotic()
        {
            var components = new AntibioticComponents("X_1_ND10");
            Assert.Equal("X_1", components.Code);
            Assert.Equal(Antibiotic.GuidelineNames.CLSI, components.Guideline);
            Assert.Equal(Antibiotic.TestMethods.Disk, components.TestMethod);

            var micComponents = new AntibioticComponents("X_2_EM");
            Assert.Equal("X_2", micComponents.Code);
            Assert.Equal(Antibiotic.GuidelineNames.EUCAST, micComponents.Guideline);
            Assert.Equal(Antibiotic.TestMethods.MIC, micComponents.TestMethod);

            var twoDigitComponents = new AntibioticComponents("X_10_NM");
            Assert.Equal("X_10", twoDigitComponents.Code);
            Assert.Equal(Antibiotic.GuidelineNames.CLSI, twoDigitComponents.Guideline);
            Assert.Equal(Antibiotic.TestMethods.MIC, twoDigitComponents.TestMethod);
        }

        [Theory]
        [InlineData("X_0_ND10")]
        [InlineData("X_00_ND10")]
        [InlineData("X_01_ND10")]
        [InlineData("X_09_FE")]
        [InlineData("X_100_ND10")]
        [InlineData("X_AB_ND10")]
        [InlineData("X_ND10")]
        [InlineData("INVALID")]
        public void AntibioticComponents_ThrowsOnInvalidCodes(string invalidCode)
        {
            Assert.Throws<System.ArgumentException>(() => new AntibioticComponents(invalidCode));
        }
    }
}

