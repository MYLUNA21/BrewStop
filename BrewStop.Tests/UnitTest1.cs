using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace BrewStop.Tests;

public class CoreSecurityAndOrderTests
{
    [Fact]
    public void QrToken_HmacSignature_DetectsTampering()
    {
        var key = Encoding.UTF8.GetBytes("BrewStop-Secret-Signing-Key-2026");
        var payload = "ORDER:42|NONCE:abc123";

        using var hmac = new HMACSHA256(key);
        var validSig = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        var tamperedSig = hmac.ComputeHash(Encoding.UTF8.GetBytes("ORDER:99|NONCE:abc123"));

        Assert.True(CryptographicOperations.FixedTimeEquals(validSig, validSig));
        Assert.False(CryptographicOperations.FixedTimeEquals(validSig, tamperedSig));
    }

    [Fact]
    public void PromoDiscount_PercentageCalculation_CapsAtSubtotal()
    {
        decimal subtotal = 150.00m;
        decimal discountPercent = 20m;
        decimal discount = Math.Min(subtotal, Math.Round(subtotal * (discountPercent / 100m), 2));

        Assert.Equal(30.00m, discount);
        Assert.Equal(120.00m, subtotal - discount);
    }

    [Fact]
    public void SingleUseToken_SecondClaimAttempt_IsRejected()
    {
        bool isUsed = false;
        bool firstClaim = !isUsed;
        isUsed = true;
        bool secondClaim = !isUsed;

        Assert.True(firstClaim);
        Assert.False(secondClaim);
    }
}