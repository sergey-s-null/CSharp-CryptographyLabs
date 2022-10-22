﻿using Autofac.Features.Indexed;
using Module.Core.Cryptography.Abstract;
using Module.DES.Entities.Abstract;
using Module.DES.Enums;
using Module.DES.Services.Abstract;

namespace Module.DES.Services;

public class DesBlockEncryptTransform : IBlockCryptoTransform
{
    public int InputBlockSize => 8;
    public int OutputBlockSize => 8;

    private readonly IDesKey _key;
    private readonly IFeistelFunctionService _feistelFunctionService;
    private readonly IUInt64BitPermutationService _initialPermutationService;
    private readonly IUInt64BitPermutationService _finalPermutationService;

    public DesBlockEncryptTransform(
        IDesKey key,
        IFeistelFunctionService feistelFunctionService,
        IIndex<PermutationType, IUInt64BitPermutationService> permutationServices)
    {
        _key = key;
        _feistelFunctionService = feistelFunctionService;
        _initialPermutationService = permutationServices[PermutationType.Initial];
        _finalPermutationService = permutationServices[PermutationType.Final];
    }

    public void Transform(ReadOnlySpan<byte> input, Span<byte> output)
    {
        ValidateArguments(input, output);

        var initialPermutationResult = _initialPermutationService.Permute(BitConverter.ToUInt64(input));

        var left = (uint)(initialPermutationResult >> 32);
        var right = (uint)(initialPermutationResult & 0xffffffff);

        for (var i = 0; i < 16; ++i)
        {
            (left, right) = (right, left ^ _feistelFunctionService.Calculate(right, _key.RoundKeys48[i]));
        }

        var concatenated = ((ulong)left << 32) | right;

        var result = _finalPermutationService.Permute(concatenated);

        unsafe
        {
            fixed (byte* outputPtr = output)
            {
                var outputULongPtr = (ulong*)outputPtr;
                *outputULongPtr = result;
            }
        }
    }

    private static void ValidateArguments(ReadOnlySpan<byte> input, Span<byte> output)
    {
        if (input.Length != sizeof(ulong))
        {
            throw new ArgumentException("Invalid length of input.", nameof(input));
        }

        if (input.Length != sizeof(ulong))
        {
            throw new ArgumentException("Invalid length of output.", nameof(input));
        }
    }
}