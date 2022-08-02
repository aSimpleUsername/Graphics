using NUnit.Framework;
using UnityEngine.TestTools.Utils;
using UnityEngine;
using UnityEditor.ShaderGraph.Defs;

namespace UnityEditor.ShaderGraph.GraphDelta.UnitTests
{

    [TestFixture]
    class RegistryPlaceholderFixture
    {
        // TODO: Decide on whether to update TestAddNode to accommodate GraphManip concretization changes
        //    [Test]// TODO: Decide on whether to update TestAddNode to accommodate GraphManip concretization changes
        //    public void RegistryPlaceholderTest()
        //    {
        //        var graph = new GraphHandler();
        //        var registry = new Registry();

        //        registry.Register<GraphType>();
        //        _ = registry.Register<TestAddNode>();
        //        registry.Register<GraphTypeAssignment>();

        //        // should default concretize length to 4.
        //        graph.AddNode<TestAddNode>("Add1", registry);
        //        var reader = graph.GetNodeReader("Add1");
        //        reader.TryGetField("In1.TypeField.Length", out GraphType.Length len);
        //        Assert.AreEqual(4, (int)len);

        //        // Set the length of input port 1 to 1.
        //        var nodeWriter = graph.GetNodeWriter("Add1");
        //        nodeWriter.SetPortField("In1", "Length", GraphType.Length.One);

        //        // After reconcretization, the node definition should propagate the length.
        //        graph.ReconcretizeNode("Add1", registry);
        //        reader = graph.GetNodeReader("Add1");
        //        reader.TryGetField("In1.TypeField.Length", out len);
        //        Assert.AreEqual(1, (int)len);
        //        reader.TryGetField("In2.TypeField.Length", out len);
        //        Assert.AreEqual(1, (int)len);
        //        reader.TryGetField("Out.TypeField.Length", out len);
        //        Assert.AreEqual(1, (int)len);

        //        // Add a second Add Node, with length 2 this time.
        //        var node2 = graph.AddNode<TestAddNode>("Add2", registry);
        //        node2.SetPortField("In2", "Length", GraphType.Length.Two);
        //        graph.ReconcretizeNode("Add2", registry);
        //        reader = graph.GetNodeReader("Add2");
        //        reader.TryGetField("In1.TypeField.Length", out len);
        //        Assert.AreEqual(2, (int)len);
        //        reader.TryGetField("In2.TypeField.Length", out len);
        //        Assert.AreEqual(2, (int)len);
        //        reader.TryGetField("Out.TypeField.Length", out len);
        //        Assert.AreEqual(2, (int)len);

        //        // Connecting Out to In should clobber the inlined length with the new length.
        //        graph.TryConnect("Add2", "Out", "Add1", "In1", registry);
        //        graph.ReconcretizeNode("Add1", registry);
        //        reader = graph.GetNodeReader("Add1");
        //        reader.TryGetPort("In1", out var portReader);
        //        portReader.GetTypeField().GetField("Length", out len);
        //        Assert.AreEqual(2, (int)len);
        //        reader.TryGetField("In2.TypeField.Length", out len);
        //        Assert.AreEqual(2, (int)len);
        //        reader.TryGetField("Out.TypeField.Length", out len);
        //        Assert.AreEqual(2, (int)len);
        //    }

        [Test]
        public void RegisterFunctionDescriptorTest()
        {
            // create the registry
            var registry = new Registry();
            registry.Register<GraphType>();

            // create the graph
            var graph = new GraphHandler();
            FunctionDescriptor fd = new(
                "Test",
                "Out = In;",
                new ParameterDescriptor[]
                {
                    new ParameterDescriptor("In", TYPE.Vector, GraphType.Usage.In),
                    new ParameterDescriptor("Out", TYPE.Vector, GraphType.Usage.Out)
                }
            );
            RegistryKey registryKey = registry.Register(fd);

            // add a single node to the graph
            string nodeName = $"{fd.Name}-01";
            graph.AddNode(registryKey, nodeName, registry);

            // check that the node was added
            var nodeReader = graph.GetNodeReader(nodeName);
            bool didRead = nodeReader.TryGetField("In.TypeField.Length", out GraphType.Length len);
            Assert.IsTrue(didRead);

            // EXPECT that both In and Out are concretized into length = 1 (default)
            Assert.AreEqual(GraphType.Length.One, len);
            didRead = nodeReader.TryGetField("Out.TypeField.Length", out len);
            Assert.IsTrue(didRead);
            Assert.AreEqual(GraphType.Length.One, len);
        }

        [Test]
        public void CanDefineNodeWithDefaultParameters()
        {
            // create registry
            var registry = new Registry();
            // register the GraphType (other types are based on it)
            registry.Register<GraphType>();
            // create a graph
            var graph = new GraphHandler();

            // define a function with an in field that has defaults
            FunctionDescriptor fd = new(
                "Test",
                "Out = In;",
                new ParameterDescriptor[]
                {
                    new ParameterDescriptor(
                        "In",
                        TYPE.Vec4,
                        GraphType.Usage.In,
                        new float[] { 1F, 1F, 3F, 1F }
                    ),
                    new ParameterDescriptor("Out", TYPE.Vector, GraphType.Usage.Out)
                }
            );
            RegistryKey registryKey = registry.Register(fd);

            // add an instance of the node to the graph
            string nodeName = "{fd.Name}-test-1";
            graph.AddNode(registryKey, nodeName, registry);

            // check that the node was added
            var nodeReader = graph.GetNodeReader(nodeName);
            bool didRead = nodeReader.TryGetField("In.TypeField.Length", out GraphType.Length _);
            Assert.IsTrue(didRead);

            // check that the value for the port made from the in param is correct
            var comparer = new FloatEqualityComparer(10e-6f);
            nodeReader.TryGetField("In.TypeField.c0", out float v);
            Assert.That(v, Is.EqualTo(1F).Using(comparer));
            nodeReader.TryGetField("In.TypeField.c1", out v);
            Assert.That(v, Is.EqualTo(1F).Using(comparer));
            nodeReader.TryGetField("In.TypeField.c2", out v);
            Assert.That(v, Is.EqualTo(3F).Using(comparer));
            nodeReader.TryGetField("In.TypeField.c3", out v);
            Assert.That(v, Is.EqualTo(1F).Using(comparer));
        }

        [Test]
        public void GradientTypeTest()
        {
            var registry = new Registry();
            registry.Register<GraphType>();
            registry.Register<GraphTypeAssignment>();
            registry.Register<GradientType>();
            registry.Register<GradientNode>();


            // check that the type initializes defaults correctly.
            var graph = new GraphHandler();
            graph.AddNode<GradientNode>("TestGradientNode", registry);
            var node = graph.GetNodeReader("TestGradientNode");
            node.TryGetPort(GradientNode.kInlineStatic, out var port);
            var actual = GradientTypeHelpers.GetGradient(port.GetTypeField());

            var expected = new Gradient
            {
                mode = GradientMode.Blend
            };
            expected.SetKeys(
                new GradientColorKey[]
                {
                    new GradientColorKey(new Color(0,0,0), 0),
                    new GradientColorKey(new Color(1,1,1), 1)
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(1, 0),
                    new GradientAlphaKey(1, 1)
                });

            Assert.AreEqual(expected, actual);

            // check to see that a basic round trip works.
            var nodeWriter = graph.GetNodeWriter("TestGradientNode");
            var field = nodeWriter.GetPort(GradientNode.kInlineStatic).GetTypeField();

            expected.mode = GradientMode.Fixed;
            expected.SetKeys(
                new GradientColorKey[]
                {
                    new GradientColorKey(new Color(0,0,0), 0),
                    new GradientColorKey(new Color(0,1,0), 1)
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(1, 0),
                    new GradientAlphaKey(0, 1)
                });
            GradientTypeHelpers.SetGradient(field, expected);


            node = graph.GetNodeReader("TestGradientNode");
            node.TryGetPort(GradientNode.kInlineStatic, out port);
            actual = GradientTypeHelpers.GetGradient(port.GetTypeField());
            Assert.AreEqual(expected, actual);
        }
    }
}
