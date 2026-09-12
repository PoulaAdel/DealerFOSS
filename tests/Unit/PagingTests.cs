// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   PagingTests — what every list endpoint does with the two numbers a caller
//   sends, proved once instead of eleven times.
//
//   Eleven services used to carry their own copy of this clamp, and two of the
//   copies disagreed about the cap. Now there is one, and this is it.
//
// Usage:
//   Runs with the normal test suite; no infrastructure required.
//
// Coding Instructions:
//   THE CAP IS NOT A COURTESY. Without it a caller can ask for every row a
//   dealership has ever written, which turns a list endpoint into a way to make
//   the database do arbitrary work. If a change here ever makes the cap
//   advisory, that is the defect.

using FluentAssertions;
using DealerFOSS.Core;
using Xunit;

namespace DealerFOSS.UnitTests;

public sealed class PagingTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-9999)]
    public void Asking_for_no_particular_size_gets_the_default(int requested)
    {
        // A caller that omits the parameter and a caller that sends 0 want the
        // same thing, and neither is an error worth refusing a list over.
        Paging.Limit(requested).Should().Be(Paging.DefaultLimit);
    }

    [Fact]
    public void A_caller_may_choose_a_smaller_page()
    {
        Paging.Limit(10).Should().Be(10);
    }

    [Theory]
    [InlineData(201)]
    [InlineData(5_000)]
    [InlineData(int.MaxValue)]
    public void Asking_for_more_than_the_cap_gets_the_cap(int requested)
    {
        Paging.Limit(requested).Should().Be(Paging.MaxLimit,
            because: "a list endpoint must not become a way to ask for everything");
    }

    [Fact]
    public void A_capability_may_lower_the_ceiling_but_not_raise_it()
    {
        // Parts asks for 100 by default; imports cap lower than the shared max.
        Paging.Limit(0, fallback: 100).Should().Be(100);
        Paging.Limit(500, max: 25).Should().Be(25);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-50)]
    public void A_negative_offset_is_the_first_page_rather_than_an_error(int requested)
    {
        // A caller bug with an obvious safe reading. Failing the request would
        // turn a mildly wrong page into an error screen.
        Paging.Offset(requested).Should().Be(0);
    }

    [Fact]
    public void An_offset_past_the_end_is_honoured_rather_than_clamped()
    {
        // Clamping it would silently hand back the last page, so a screen that
        // asked for row 10,000 of 400 would show rows and look fine. Empty is
        // the honest answer, and the total says why.
        Paging.Offset(10_000).Should().Be(10_000);
    }

    [Fact]
    public void A_page_knows_whether_asking_again_is_worth_it()
    {
        var middle = new Page<string>(["a", "b"], Total: 10, Offset: 0, Limit: 2);
        var last = new Page<string>(["i", "j"], Total: 10, Offset: 8, Limit: 2);
        var past = new Page<string>([], Total: 10, Offset: 99, Limit: 2);

        middle.HasMore.Should().BeTrue();
        last.HasMore.Should().BeFalse(because: "offset 8 plus two rows is all ten");
        past.HasMore.Should().BeFalse();
    }

    [Fact]
    public void A_short_last_page_still_knows_it_is_the_last()
    {
        // The arithmetic that matters: rows returned, not the limit asked for.
        // Using the limit here would say "there is more" on every final page
        // that happens not to be full.
        new Page<string>(["z"], Total: 51, Offset: 50, Limit: 50)
            .HasMore.Should().BeFalse();
    }
}
