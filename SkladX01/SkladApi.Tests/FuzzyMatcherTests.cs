using SkladApi.Services;
using Xunit;

namespace SkladApi.Tests;

public class FuzzyMatcherTests
{
    [Fact]
    public void Identical_after_normalization_is_100()
    {
        Assert.Equal(100, FuzzyMatcher.Similarity("Шайба Цинк_10", "шайба цинк 10"));
    }

    [Fact]
    public void Similar_spelling_passes_threshold()
    {
        Assert.True(FuzzyMatcher.Similarity("саморез 4.2х75", "Саморезы_4,2 х 75") >= 75);
    }

    [Fact]
    public void Different_numbers_conflict()
    {
        Assert.True(FuzzyMatcher.NumbersConflict("шайба цинк 10", "Шайба Цинк_8"));
    }

    [Fact]
    public void Same_numbers_with_different_separators_do_not_conflict()
    {
        Assert.False(FuzzyMatcher.NumbersConflict("саморез 4.2х75", "Саморезы_4,2 х 75"));
    }

    [Fact]
    public void Empty_source_gives_zero()
    {
        Assert.Equal(0, FuzzyMatcher.Similarity(null, "Шайба Цинк_10"));
    }
}
