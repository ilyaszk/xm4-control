using Xm4Control.Core.Protocol;

namespace Xm4Control.Tests;

/// <summary>Payloads capturés sur un WH-1000XM4 (firmware 2.9.0).</summary>
public class ProtocolTests
{
    [Theory]
    [InlineData(AmbientMode.NoiseCancelling, 15, false, "6802110202010000")]
    [InlineData(AmbientMode.WindReduction, 15, false, "6802110201010000")]
    [InlineData(AmbientMode.Ambient, 15, true, "680211020001010F")]
    [InlineData(AmbientMode.Off, 15, true, "680200020001000F")]
    [InlineData(AmbientMode.Ambient, 0, false, "6802110200010001")]
    [InlineData(AmbientMode.Ambient, 99, false, "6802110200010014")]
    public void SetAmbient_Payload(AmbientMode mode, int level, bool voice, string expected)
    {
        var request = Commands.SetAmbient(new AmbientSettings(mode, level, voice));

        Assert.Equal(MessageType.Command1, request.Type);
        Assert.Equal(expected, Convert.ToHexString(request.Payload));
    }

    [Fact]
    public void SetEqualizerBands_OffsetsValuesBy10()
    {
        var request = Commands.SetEqualizerBands([-10, 0, 3, -2, 10, 1]);

        Assert.Equal("5801FF06000A0D0814" + "0B", Convert.ToHexString(request.Payload));
    }

    [Theory]
    [InlineData("6702010202010000", AmbientMode.NoiseCancelling, 0, false)]
    [InlineData("670201020001010F", AmbientMode.Ambient, 15, true)]
    [InlineData("690201020101000F", AmbientMode.WindReduction, 15, false)]
    [InlineData("670200020001010F", AmbientMode.Off, 15, true)]
    public void Parse_Ambient(string hex, AmbientMode mode, int level, bool voice)
    {
        var state = Parse(MessageType.Command1, hex);

        Assert.Equal(new AmbientSettings(mode, level, voice), state.Ambient);
    }

    [Fact]
    public void Parse_DeviceInfo()
    {
        var state = new Xm4State();
        foreach (var hex in new[] { "01007000", "050205322E392E30", "11003C00", "190002", "570100060A0A0A0A0A0A" })
            state = ResponseParser.Apply(state, MessageType.Command1, Convert.FromHexString(hex));

        Assert.Equal(1, state.ProtocolVersion);
        Assert.Equal("2.9.0", state.Firmware);
        Assert.Equal(60, state.Battery);
        Assert.False(state.Charging);
        Assert.Equal("AAC", state.Codec);
        Assert.Equal(EqPreset.Off, state.EqPreset);
        Assert.Equal([0, 0, 0, 0, 0, 0], state.EqBands!);
    }

    [Theory]
    [InlineData("F7050001", true)]   // GET
    [InlineData("F7050000", false)]
    [InlineData("F9050101", true)]   // notification après SET
    public void Parse_SpeakToChat(string hex, bool expected)
    {
        Assert.Equal(expected, Parse(MessageType.Command1, hex).SpeakToChat);
    }

    [Fact]
    public void Parse_IgnoresSpeakToChatStatusMessage()
    {
        Assert.Null(Parse(MessageType.Command1, "F9050200").SpeakToChat);
    }

    [Fact]
    public void Parse_Settings()
    {
        Assert.True(Parse(MessageType.Command1, "F7030001").PauseWhenTakenOff);
        Assert.False(Parse(MessageType.Command1, "E7020000").Dsee);
        Assert.True(Parse(MessageType.Command2, "47010101").VoiceGuidance);
    }

    [Theory]
    [InlineData("D7D10101", true)]   // GET : pavé tactile actif
    [InlineData("D9D10100", false)]  // notification après SET D8 D1 01 00
    public void Parse_TouchSensor(string hex, bool expected)
    {
        Assert.Equal(expected, Parse(MessageType.Command1, hex).TouchSensor);
    }

    [Fact]
    public void Parse_TouchSensor_IgnoresSetting2()
    {
        // 0xD2 n'existe pas sur le XM4 (pas de réponse à D0 D2), même s'il répond D7 D2 01 00.
        Assert.Null(Parse(MessageType.Command1, "D7D20100").TouchSensor);
    }

    [Theory]
    [InlineData("F704011000", true)]
    [InlineData("F904011100", false)]
    public void Parse_AutoPowerOff(string hex, bool expected)
    {
        Assert.Equal(expected, Parse(MessageType.Command1, hex).AutoPowerOff);
    }

    [Fact]
    public void Parse_SpeakToChatConfig()
    {
        var config = Parse(MessageType.Command1, "FB0500000001").SpeakToChatConfig;

        Assert.Equal(new SpeakToChatConfig(SpeakToChatSensitivity.Auto, false, SpeakToChatTimeout.Standard), config);
    }

    [Theory]
    [InlineData("85010001", NcOptimizerStatus.CheckingFit)]
    [InlineData("85010010", NcOptimizerStatus.Analyzing)]
    [InlineData("85010011", NcOptimizerStatus.Finished)]
    public void Parse_NcOptimizerStatus(string hex, NcOptimizerStatus expected)
    {
        Assert.Equal(expected, Parse(MessageType.Command1, hex).NcOptimizer);
    }

    [Fact]
    public void SettingsCommands_Payloads()
    {
        Assert.Equal("D6D1", Convert.ToHexString(Commands.GetTouchSensor().Payload));
        Assert.Equal("D8D10100", Convert.ToHexString(Commands.SetTouchSensor(false).Payload));
        Assert.Equal("F804011000", Convert.ToHexString(Commands.SetAutoPowerOff(true).Payload));
        Assert.Equal("F804011100", Convert.ToHexString(Commands.SetAutoPowerOff(false).Payload));
        Assert.Equal("FC0500020103", Convert.ToHexString(Commands.SetSpeakToChatConfig(
            new SpeakToChatConfig(SpeakToChatSensitivity.Low, true, SpeakToChatTimeout.Off)).Payload));
        Assert.Equal("84010001", Convert.ToHexString(Commands.StartNcOptimizer(true).Payload));
    }

    [Fact]
    public void Parse_UnknownPayload_KeepsState()
    {
        var state = new Xm4State { Battery = 42 };

        Assert.Same(state, ResponseParser.Apply(state, MessageType.Command1, [0x91, 0x00]));
    }

    private static Xm4State Parse(MessageType type, string hex) =>
        ResponseParser.Apply(new Xm4State(), type, Convert.FromHexString(hex));
}
