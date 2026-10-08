# Browser physics compiler compatibility

The .NET 9 WebAssembly AOT compiler asserts while compiling float-vector comparison masks used by Bepu collision tasks. In particular, `TriangleCylinderTester.TryAddInteriorPoint` adds `Vector<int>` masks returned by float comparisons; the LLVM lowering produces inconsistent operand types.

This build-time tool replaces only the five float-vector comparison operations which return integer masks, in the generated trimmed browser Bepu assemblies. The replacements are scoped to collision tasks and BepuUtilities; solver constraint comparisons stay unchanged. Each replacement calls the original System.Numerics comparison through a non-inlined method boundary. The boundary isolates its integer-mask return from the compiler's problematic inlined expression lowering. The helper must remain non-inlined so Mono does not reintroduce the problematic SIMD lowering. Vector arithmetic and the solver continue to use SIMD. Bepu's version, solver parameters, joints, contacts and integration are unchanged. Desktop assemblies and the NuGet cache are never patched.

`AdaptBrowserPhysicsMasks` runs before the SDK's AOT compile target. The emitted helper lives inside each adapted assembly; the browser does not ship Mono.Cecil or this tool. The patch is idempotent and rejects paths outside generated `obj` directories.

Run the equivalence check with:

```
dotnet run --project Tools/BrowserPhysicsCompatibility -c Release -- --self-test
```

It checks 5,000 comparisons against System.Numerics, including NaN, infinities and signed zero. Set `DOTNET_EnableAVX2=0` for a second run on AVX2 PCs to check four-lane comparisons; an ordinary run checks eight-lane comparisons. Actual browser driving must also be tested after publishing. Reevaluate this workaround when updating the .NET WASM toolchain; do not suppress a failed AOT build by interpreting the physics solver.
