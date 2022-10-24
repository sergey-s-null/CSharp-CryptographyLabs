﻿using System.Numerics;
using Autofac.Features.AttributeFilters;
using Module.RSA;
using Module.RSA.Entities;
using Module.RSA.Entities.Abstract;
using Module.RSA.Enums;
using Module.RSA.Exceptions;
using Module.RSA.Services.Abstract;
using Util.RSA.WienerAttackTest.Entities;
using Util.RSA.WienerAttackTest.Entities.Abstract;
using Util.RSA.WienerAttackTest.Services.Abstract;

namespace Util.RSA.WienerAttackTest.Services;

public class WienerAttackTestService : IWienerAttackTestService
{
    private readonly IWienerAttackTestServiceParameters _parameters;
    private readonly IRSAKeyPairGenerator _rsaKeyPairGenerator;
    private readonly Func<IPrimesPairGeneratorParameters, IPrimesPairGenerator> _primesPairGeneratorFactory;
    private readonly Func<IWienerAttackStatisticsCollector, IRSAAttackService> _attackServiceFactory;

    public WienerAttackTestService(
        IWienerAttackTestServiceParameters parameters,
        [KeyFilter(RSAKeyPairGenerationType.WithWienerAttackVulnerability)]
        IRSAKeyPairGenerator rsaKeyPairGenerator,
        Func<IPrimesPairGeneratorParameters, IPrimesPairGenerator> primesPairGeneratorFactory,
        [KeyFilter(RSAAttackType.Wiener)]
        Func<IWienerAttackStatisticsCollector, IRSAAttackService> attackServiceFactory)
    {
        _parameters = parameters;
        _rsaKeyPairGenerator = rsaKeyPairGenerator;
        _primesPairGeneratorFactory = primesPairGeneratorFactory;
        _attackServiceFactory = attackServiceFactory;
    }

    public async Task<IWienerAttackComplexTestResult> PerformComplexTestAsync()
    {
        var results = new List<IWienerAttackTestResult>();
        foreach (var byteCount in _parameters.ByteCounts)
        {
            var result = await PerformTestAsync(byteCount);
            results.Add(result);
        }

        return new WienerAttackComplexTestResult(results);
    }

    private async Task<IWienerAttackTestResult> PerformTestAsync(int byteCount)
    {
        var primesPairGenerator = GetPrimesPairGenerator(byteCount);

        var result = new WienerAttackTestResult
        {
            ByteCount = byteCount,
            AttackCount = _parameters.AttackCount,
            MinExponentsCheckCount = int.MaxValue,
            MaxExponentsCheckCount = int.MinValue
        };

        var totalExponentsCheckCount = 0;

        for (var i = 0; i < _parameters.AttackCount; i++)
        {
            primesPairGenerator.Generate(out var p, out var q);
            var keyPair = _rsaKeyPairGenerator.Generate(p, q);

            var statistics = new WienerAttackStatistics();
            var attackService = _attackServiceFactory(statistics);

            var (success, foundPrivateExponent) = await AttackAsync(attackService, keyPair.Public);
            if (success)
            {
                totalExponentsCheckCount += statistics.ExponentsCheckedCount;

                if (statistics.ExponentsCheckedCount < result.MinExponentsCheckCount)
                {
                    result.MinExponentsCheckCount = statistics.ExponentsCheckedCount;
                }

                if (statistics.ExponentsCheckedCount > result.MaxExponentsCheckCount)
                {
                    result.MaxExponentsCheckCount = statistics.ExponentsCheckedCount;
                }

                if (foundPrivateExponent != keyPair.Private.Exponent)
                {
                    result.MissCount++;
                }
            }
            else
            {
                result.ErrorCount++;
            }
        }

        result.AverageExponentsCheckCount = (double)totalExponentsCheckCount / _parameters.AttackCount;

        return result;
    }

    private IPrimesPairGenerator GetPrimesPairGenerator(int byteCount)
    {
        return _primesPairGeneratorFactory(new PrimesPairGeneratorParameters(
            byteCount,
            byteCount * 8 - 1,
            1000
        ));
    }

    private static async Task<(bool, BigInteger)> AttackAsync(IRSAAttackService attackService, IRSAKey publicKey)
    {
        try
        {
            var foundPrivateExponent = await attackService.AttackAsync(
                publicKey.Exponent,
                publicKey.Modulus
            );

            return (true, foundPrivateExponent);
        }
        catch (CryptographyAttackException)
        {
            return (false, BigInteger.Zero);
        }
    }
}