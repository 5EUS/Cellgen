# The Cellgen random stream

Generation is deterministic because every random number comes from one specified stream:
**PCG32**, seeded per stage. This document is the specification. Any implementation that
reproduces `spec/rng_vectors.txt` and `spec/rng_helpers.txt` produces the same levels and
the same script behaviour as this library, on any machine. The C# reference is
`src/Cellgen/Generation/GenRng.cs`.

`spec/rng_vectors.txt` was recorded from Godot's `RandomNumberGenerator`, which Cellgen's
first host used, so seeds from before this library existed still build the same levels.
`spec/rng_helpers.txt` is recorded from the C# reference. `GenRngTests` and `RngSpecTests`
check them; other implementations should check them too.

## Core

All arithmetic is modulo 2^64 (state) or 2^32 (output). `inc` is a 64-bit odd constant.

```
DEFAULT_INC = 1442695040888963407
MULTIPLIER  = 6364136223846793005

new(seed):
    state = 0
    inc   = (DEFAULT_INC << 1) | 1
    next_u32()
    state += seed
    next_u32()

next_u32():
    old   = state
    state = old * MULTIPLIER + inc
    xorshifted = ((old >> 18) ^ old) >> 27        # truncated to 32 bits
    rot        = old >> 59
    return rotate_right_32(xorshifted, rot)
```

## Derived draws

```
bounded(bound):                      # unbiased value in [0, bound)
    threshold = (-bound as u32) % bound
    loop: r = next_u32(); if r >= threshold: return r % bound

range(min, max):                     # inclusive; max is raised to min if lower
    max = max(min, max)
    if min == max: return min
    bound = (max - min + 1) as u32
    return min + bounded(bound)

next(count):  count <= 1 ? 0 : range(0, count - 1)

float():                             # f32, in [0, 1]
    x = next_u32()
    if x == 0: return 0
    s = next_u32() | 0x80000001
    return (f32)( (f64)s * 2^(-32 - clz32(x)) )

chance(p):    p >= 1 || (p > 0 && float() < p)

next_seed():  (next_u32() << 32) | next_u32()          # high word drawn first
```

`clz32` counts leading zero bits of a 32-bit value. `float()` consumes two words except
when the first is zero.

## Weighted pick and shuffle

All arithmetic here is `f32`. Weights below zero count as zero.

```
pick_weighted(weights):              # index, or -1 when every weight is zero
    total = sum(max(0, w))           # left to right
    if total <= 0: return -1
    roll = float() * total
    for i in 0..n: roll -= max(0, w[i]); if roll <= 0 && w[i] > 0: return i
    for i in n-1 down to 0: if w[i] > 0: return i      # rounding leftovers

shuffle(list):                       # Fisher-Yates, from the end
    for i in len-1 down to 1: j = next(i + 1); swap(list[i], list[j])
```

## Not in the portable spec

`GenRng.WeightedOrder` (the solver's candidate ordering) uses `Math.Pow` on doubles, whose
last bit is not guaranteed identical across runtimes and operating systems. It is used
inside the solver only; scripts and ports do not need it, and it is not part of the
vectors.

## Per-stage streams

A stage's stream is `new(StableHash.Derive(seed, stage))`. `StableHash` is FNV-1a over the
UTF-8 bytes of the label, started from `0xcbf29ce484222325 ^ mix(seed)` and finished with
`mix`, where

```
mix(v): v += 0x9E3779B97F4A7C15
        v = (v ^ (v >> 30)) * 0xBF58476D1CE4E5B9
        v = (v ^ (v >> 27)) * 0x94D049BB133111EB
        return v ^ (v >> 31)
```

A host running a script gives it one 64-bit seed drawn with `next_seed()` from the stage's
stream, so a script's draws never share state with the host's.

## Changing the spec

A change to any rule above changes every level every seed builds. Treat it as a format
break: regenerate the vectors deliberately (`CELLGEN_WRITE_SPEC=1 dotnet test` for the
helpers), say so in the change, and expect every host's golden fingerprints to move.
