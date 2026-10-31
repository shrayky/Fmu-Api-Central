using CSharpFunctionalExtensions;
using Domain.Vetis;

namespace Domain.Tests;

public class VetisQuenchTests
{
    [Theory]
    [InlineData("CONFIRMED", true)]
    [InlineData("confirmed", true)]
    [InlineData("UTILIZED", false)]
    [InlineData("FINALIZED", false)]
    [InlineData("WITHDRAWN", false)]
    [InlineData("CANCELED", false)]
    [InlineData("", false)]
    public void Гасится_только_оформленный(string status, bool expected) =>
        Assert.Equal(expected, VetisQuench.CanQuench(status));

    [Fact]
    public void Полный_объём_принимается_целиком()
    {
        var order = VetisQuench.Decide("12.5", "12.5").Value;

        Assert.Equal(VetisQuench.AcceptAll, order.Decision);
        Assert.True(order.KeepIncomingVolume);
    }

    [Fact]
    public void Пропущенный_факт_считается_полным_объёмом()
    {
        var order = VetisQuench.Decide("10", null).Value;

        Assert.Equal(VetisQuench.AcceptAll, order.Decision);
        Assert.Equal("10", order.IncomingVolume);
    }

    [Fact]
    public void Переданный_пустой_факт_не_гасится()
    {
        Assert.True(VetisQuench.Decide("10", "").IsFailure);
        Assert.True(VetisQuench.Decide("10", " ").IsFailure);
    }

    [Fact]
    public void Объём_в_акте_пишется_с_точкой()
    {
        var order = VetisQuench.Decide("10,5", "4,5").Value;

        Assert.Equal("10.5", order.IncomingVolume);
        Assert.Equal("4.5", order.ActualVolume);
    }

    [Fact]
    public void Разница_даёт_частичную_приёмку()
    {
        var order = VetisQuench.Decide("10", "4,5").Value;

        Assert.Equal(VetisQuench.Partially, order.Decision);
        Assert.Equal("4.5", order.ActualVolume);
        Assert.False(order.KeepIncomingVolume);
    }

    [Fact]
    public void Ноль_возвращает_всю_партию()
    {
        var order = VetisQuench.Decide("10", "0").Value;

        Assert.Equal(VetisQuench.ReturnAll, order.Decision);
        Assert.True(order.KeepIncomingVolume);
        Assert.Equal("0", order.ActualVolume);
    }

    [Fact]
    public void Факт_вне_объёма_не_гасится()
    {
        Assert.True(VetisQuench.Decide("10", "11").IsFailure);
        Assert.True(VetisQuench.Decide("10", "-1").IsFailure);
        Assert.True(VetisQuench.Decide("", "1").IsFailure);
        Assert.True(VetisQuench.Decide("10", "много").IsFailure);
    }
}
