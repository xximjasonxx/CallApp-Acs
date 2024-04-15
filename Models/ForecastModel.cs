using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CallApp.Models;

public record ForecastModel
{
    public DateTime Date { get; init; }
    public TemperatureModel Temperature { get; init; }
    public DayPartForecastModel Day { get; init; }
    public DayPartForecastModel Night { get; init; }

    public override string ToString()
    {
        return @$"
            During the day the weather will be {Day.ShortPhrase} with a {Day.PrecipitationProbability}% chance of precipitation.
            At night the weather will be {Night.ShortPhrase} with a {Night.PrecipitationProbability}% chance of precipitation.
            The temperature will range from {Temperature.Minimum.Value}°{Temperature.Minimum.Unit} to {Temperature.Maximum.Value}°{Temperature.Maximum.Unit}.";
    }
}

public record TemperatureModel
{
    public TempUnitModel Minimum { get; init; }
    public TempUnitModel Maximum { get; init; }
}

public record TempUnitModel
{
    public decimal Value { get; init; }
    public char Unit { get; init; }
}

public record DayPartForecastModel
{
    public string ShortPhrase { get; init; }
    public int PrecipitationProbability { get; init; }
}