using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;
using NUnit.Framework.Internal.Builders;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Rendering;
using UnityEngine.TestTools.Graphics;
using UnityEngine.XR.Management;
//using UnityEngine.XR.Management;
using Attribute = System.Attribute;

public class UseTestAssetTestCaseAttribute : UnityEngine.TestTools.UnityTestAttribute, ITestBuilder
{

    [System.Serializable]
    public class TestAssetTestData
    {
        public string testName;
        [NonSerialized]
        public Texture2D expectedResult;
        [SerializeField]
        public string ExpectedResultPath { get; private set; }
        public int expectedHash;
        [NonSerialized]
        public Material testMaterial;
        [SerializeField]
        public string TestMaterialPath { get; private set; }
        public int testHash;
        public bool isCameraPersective;
        [NonSerialized]
        public ImageComparisonSettings imageComparisonSettings;
        [SerializeField]
        private string json_imageComp;
        [NonSerialized]
        public Mesh customMesh;
        [SerializeField]
        public string CustomMeshPath { get; private set; }

        public string ToJson()
        {
            json_imageComp = JsonUtility.ToJson(imageComparisonSettings);
            return JsonUtility.ToJson(this);
        }

        public void FromJson(string json)
        {
            JsonUtility.FromJsonOverwrite(json, this);
            imageComparisonSettings = JsonUtility.FromJson<ImageComparisonSettings>(json_imageComp);
        }

        public TestAssetTestData()
        {

        }

        public TestAssetTestData(ShaderGraphTestAsset testAsset, ShaderGraphTestAsset.MaterialTest individualTest, Texture2D expectedResultImage, int expectedResultHash)
        {
            testName = testAsset.name;
            expectedResult = expectedResultImage;
            expectedHash = expectedResultHash;
            testMaterial = individualTest.material;
            testHash = individualTest.hash;
            isCameraPersective = testAsset.isCameraPerspective;
            imageComparisonSettings = testAsset.settings;
            customMesh = testAsset.customMesh;
            ExpectedResultPath = $"{testName}_{testMaterial.name}_image";
            TestMaterialPath = $"{testName}_{testMaterial.name}_material";
            if(customMesh == null)
            {
                CustomMeshPath = null;
            }
            else
            {
                CustomMeshPath = $"{testName}_mesh";
            }
        }
    }

    public interface ITestAssetTestProvider
    {
        public IEnumerable<TestAssetTestData> GetTestCases();
    }

    public static string LoadedXRDevice
    {
        get
        {
#if ENABLE_VR || ENABLE_AR
            // Reuse standard (non-VR) reference images
            if (RuntimeSettings.reuseTestsForXR)
                return "None";

            // XR SDK path
            var activeLoader = XRGeneralSettings.Instance?.Manager?.activeLoader;
            if (activeLoader != null)
                return activeLoader.name;

            // Legacy VR path
            if (UnityEngine.XR.XRSettings.enabled && UnityEngine.XR.XRSettings.loadedDeviceName.Length > 0)
                return UnityEngine.XR.XRSettings.loadedDeviceName;

#endif
            return "None";
        }
    }

#if UNITY_EDITOR
    public class EditorProvider : ITestAssetTestProvider
    {
        static string k_fileLocation = $"Assets/ReferenceImages/{QualitySettings.activeColorSpace}/{Application.platform}/{SystemInfo.graphicsDeviceType}/{LoadedXRDevice}";
        public IEnumerable<TestAssetTestData> GetTestCases()
        {
            List<TestAssetTestData> output = new List<TestAssetTestData>();
            foreach(var testAsset in SetupTestAssetTestCases.ShaderGraphTests)
            {
                foreach(var individualTest in testAsset.testMaterial)
                {
                    var hashPath = $"{k_fileLocation}/{testAsset.name}/{individualTest.material.name}{SetupTestAssetTestCases.k_resultHashSuffix}";
                    if(!File.Exists(hashPath))
                    {
                        continue;
                    }

                    TestAssetTestData data = new TestAssetTestData();
                    data.FromJson(File.ReadAllText(hashPath));
                    data.expectedResult = AssetDatabase.LoadAssetAtPath<Texture2D>($"{k_fileLocation}/{testAsset.name}/{individualTest.material.name}{SetupTestAssetTestCases.k_resultImageSuffix}");
                    data.testMaterial = individualTest.material;
                    data.customMesh = testAsset.customMesh;
                    if(data.expectedResult == null || data.testMaterial == null)
                    {
                        continue;
                    }
                    output.Add(data);
                }
            }
            return output;
        }
    }
#else

    public class PlayerProvider : ITestAssetTestProvider
    {
        public IEnumerable<TestAssetTestData> GetTestCases()
        {
            AssetBundle referenceImagesBundle = null;

            // apparently unity automatically saves the asset bundle as all lower case
            var referenceImagesBundlePath = string.Format("referenceimages-{0}-{1}-{2}-{3}",
                UseGraphicsTestCasesAttribute.ColorSpace,
                UseGraphicsTestCasesAttribute.Platform,
                UseGraphicsTestCasesAttribute.GraphicsDevice,
                UseGraphicsTestCasesAttribute.LoadedXRDevice).ToLower();

            referenceImagesBundlePath = Path.Combine(Application.streamingAssetsPath, referenceImagesBundlePath);

#if UNITY_ANDROID
            // Unlike standalone where you can use File.Read methods and pass the path to the file,
            // Android requires UnityWebRequest to read files from local storage
            referenceImagesBundle = GetRefImagesBundleViaWebRequest(referenceImagesBundlePath);

#else
            if (File.Exists(referenceImagesBundlePath))
                referenceImagesBundle = AssetBundle.LoadFromFile(referenceImagesBundlePath);

#endif

            if (referenceImagesBundle != null)
            {
                foreach (TextAsset individualTestData in referenceImagesBundle.LoadAllAssets(typeof(TextAsset)))
                {
                    TestAssetTestData data = new TestAssetTestData();
                    data.FromJson(individualTestData.text);
                    data.expectedResult = referenceImagesBundle.LoadAsset<Texture2D>(data.ExpectedResultPath);
                    data.testMaterial = referenceImagesBundle.LoadAsset<Material>(data.TestMaterialPath);
                    if(data.CustomMeshPath != null)
                    {
                        data.customMesh = referenceImagesBundle.LoadAsset<Mesh>(data.CustomMeshPath);
                    }

                    yield return data;
                }
            }
        }

        private AssetBundle GetRefImagesBundleViaWebRequest(string referenceImagesBundlePath)
        {
            AssetBundle referenceImagesBundle = null;
            using (var webRequest = new UnityWebRequest(referenceImagesBundlePath))
            {
                var handler = new DownloadHandlerAssetBundle(referenceImagesBundlePath, 0);
                webRequest.downloadHandler = handler;

                webRequest.SendWebRequest();

                while (!webRequest.isDone)
                {
                    // wait for response
                }

                if (string.IsNullOrEmpty(webRequest.error))
                {
                    referenceImagesBundle = handler.assetBundle;
                }
                else
                {
                    Debug.Log("Error loading reference image bundle, " + webRequest.error);
                }
            }
            return referenceImagesBundle;
        }
    }
#endif

    public ITestAssetTestProvider Provider
    {
        get
        {
#if UNITY_EDITOR
            return new EditorProvider();
#else
            return new PlayerProvider();
#endif
        }
    }


    NUnitTestCaseBuilder m_builder = new NUnitTestCaseBuilder();

    IEnumerable<TestMethod> ITestBuilder.BuildFrom(IMethodInfo method, Test suite)
    {
        List<TestMethod> results = new List<TestMethod>();

        foreach (var materialTest in Provider.GetTestCases())
        {
            if (materialTest.testMaterial == null || materialTest.testName == null || materialTest.testName.Length == 0)
            {
                continue;
            }

            TestCaseData data = new TestCaseData(new object[] {materialTest.testMaterial, materialTest.isCameraPersective, materialTest.expectedResult, materialTest.imageComparisonSettings, materialTest.customMesh });
            data.SetName(materialTest.testMaterial.name);
            data.ExpectedResult = new UnityEngine.Object();
            data.HasExpectedResult = true;
            data.SetCategory(materialTest.testName);

            TestMethod test = this.m_builder.BuildTestMethod(method, suite, data);
            if (test.parms != null)
                test.parms.HasExpectedResult = false;

            test.Name = $"{materialTest.testName} {materialTest.testMaterial.name}";
            results.Add(test);
        }

        return results;
    }


}

