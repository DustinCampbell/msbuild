// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;

using Microsoft.Build.Evaluation.Expander;

using Shouldly;

using Xunit;

namespace Microsoft.Build.Engine.UnitTests.Evaluation
{
    public class FunctionArguments_Tests
    {
        [Fact]
        public void ReadsRawArgumentsWithoutMaterializing()
        {
            FunctionArguments arguments = new(["first", "second"]);

            arguments.GetValue(0).ShouldBe("first");
            arguments.GetValue(1).ShouldBe("second");
        }

        [Fact]
        public void MaterializesArgumentsOnDemand()
        {
            FunctionArguments arguments = new(["first", "second"]);
            var materializer = new TrackingMaterializer(index => $"expanded-{index}");
            arguments.SetMaterializer(materializer);

            arguments.GetValue(1).ShouldBe("expanded-1");
            materializer.Indices.ShouldBe([1]);

            arguments.GetValue(0).ShouldBe("expanded-0");
            materializer.Indices.ShouldBe([1, 0]);

            arguments.GetValue(1).ShouldBe("expanded-1");
            materializer.Indices.ShouldBe([1, 0]);
        }

        [Fact]
        public void MaterializesAllArgumentsOnce()
        {
            FunctionArguments arguments = new(["first", "second"]);
            var materializer = new TrackingMaterializer(index => $"expanded-{index}");
            arguments.SetMaterializer(materializer);

            arguments.MaterializeAll().ShouldBe(["expanded-0", "expanded-1"]);
            arguments.MaterializeAll().ShouldBe(["expanded-0", "expanded-1"]);
            materializer.Indices.ShouldBe([0, 1]);
        }

        [Fact]
        public void SnapshotDoesNotMaterializeArguments()
        {
            FunctionArguments arguments = new(["first", "second"]);
            var materializer = new TrackingMaterializer(index => $"expanded-{index}");
            arguments.SetMaterializer(materializer);

            arguments.GetValue(1).ShouldBe("expanded-1");

            arguments.SnapshotValues().ShouldBe(["first", "expanded-1"]);
            materializer.Indices.ShouldBe([1]);
        }

        [Fact]
        public void CachesMaterializedNull()
        {
            FunctionArguments arguments = new(["value"]);
            var materializer = new TrackingMaterializer(_ => null);
            arguments.SetMaterializer(materializer);

            arguments.GetValue(0).ShouldBeNull();
            arguments.GetValue(0).ShouldBeNull();
            materializer.Indices.ShouldBe([0]);
        }

        private sealed class TrackingMaterializer : IFunctionArgumentMaterializer
        {
            private readonly Func<int, object?> _materialize;

            internal TrackingMaterializer(Func<int, object?> materialize)
            {
                _materialize = materialize;
            }

            internal List<int> Indices { get; } = [];

            public object? Materialize(string? source, int index)
            {
                Indices.Add(index);
                return _materialize(index);
            }
        }
    }
}
