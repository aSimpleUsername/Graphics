using System.Collections.Generic;
using static UnityEditor.ShaderGraph.Registry.Types.GraphType;

namespace com.unity.shadergraph.defs
{

    internal class Matrix2Node : IStandardNode
    {
        public static FunctionDescriptor FunctionDescriptor => new(
            1,     // Version
            "Matrix2x2", // Name
            "Out = Matrix2x2;",
            new ParameterDescriptor("Matrix2x2", TYPE.Mat2, Usage.Static),
            new ParameterDescriptor("Out", TYPE.Mat2, Usage.Out)
        );

        public static Dictionary<string, string> UIStrings => new()
        {
            { "Category", "Input, Matrix" },
            { "Tooltip", "creates a static 2x2 matrix" },
            { "Parameters.Out.Tooltip", "a 2x2 matrix" }
        };
    }
}
