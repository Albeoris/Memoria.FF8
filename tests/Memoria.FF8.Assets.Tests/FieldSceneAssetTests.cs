using System.Buffers.Binary;
using System.Numerics;
using System.Text;
using System.Text.Json.Nodes;
using Memoria.FF8.Assets;

namespace Memoria.FF8.Assets.Tests;

public sealed class FieldSceneAssetTests
{
    [Fact]
    public void ImportsSelectedSceneWithCamerasMaterialsAndIndependentClips()
    {
        FieldSceneAsset scene = FieldSceneAsset.ImportGlb(CreateGlb());
        Assert.Equal("room", scene.Id);
        Assert.Equal(4, scene.Geometry.Bones.Count);
        Assert.Empty(scene.Geometry.Textures);
        FieldSceneMaterial material = Assert.Single(scene.Materials);
        Assert.Equal("glass", material.Id);
        Assert.True(material.Unlit);
        Assert.True(material.DoubleSided);
        Assert.Equal(FieldSceneAlphaMode.Blend, material.AlphaMode);
        Assert.Equal(new Vector4(0.2f, 0.3f, 0.4f, 0.6f), material.BaseColorFactor);
        Assert.Equal(Vector4.One, scene.Geometry.Meshes[0].Vertices[0].Color);
        Assert.Equal(new[] { "node/Root/window/primitive/geometry", "node/Root/desk/primitive/geometry" }, scene.Primitives.Select(primitive => primitive.Id));
        Assert.All(scene.Geometry.Meshes, mesh => Assert.Equal(new[] { 0, 1, 2 }, mesh.Indices));
        FieldSceneCamera perspective = scene.Cameras[0];
        Assert.Equal("main", perspective.Id);
        Assert.Equal(16f / 9f, perspective.AspectRatio!.Value, 5);
        Assert.Equal(new Vector2(0.4f, 0.6f), perspective.PrincipalPoint);
        Assert.Null(perspective.FarPlane);
        FieldSceneCamera orthographic = scene.Cameras[1];
        Assert.True(orthographic.Orthographic);
        Assert.Equal(2, orthographic.YMagnitude);
        Assert.Equal(0, orthographic.NearPlane);
        Assert.Equal(20, orthographic.FarPlane);
        Assert.Equal(new Vector3(2, 3, 0), scene.SampleTransforms(-1, 0)[perspective.Node].Translation);
        FieldSceneClip clip = Assert.Single(scene.Clips);
        Assert.Equal("opening", clip.Id);
        Assert.Equal(1, clip.StartTime);
        Assert.Equal(2, clip.Duration);
        Matrix4x4[] sampled = scene.SampleTransforms(clip.AnimationIndex, 2);
        Assert.Equal(new Vector3(4, 3, 0), sampled[perspective.Node].Translation);
        Assert.Equal(new Vector3(4, 3, 0), ModelExchange.PoseWithTransforms(scene.Geometry, sampled)[0][0]);
        Assert.Equal(Vector3.UnitZ, scene.PoseNormals(sampled)[0][0]);
    }

    [Fact]
    public void RejectsAmbiguousIdsUnsupportedRequiredExtensionsAndReachableMissingImages()
    {
        InvalidDataException duplicate = Assert.Throws<InvalidDataException>(() => FieldSceneAsset.ImportGlb(CreateGlb(root => root["nodes"]![3]!["name"] = "window")));
        Assert.Contains("ambiguous", duplicate.Message);
        Assert.Throws<InvalidDataException>(() => FieldSceneAsset.ImportGlb(CreateGlb(root => root["extensionsRequired"] = new JsonArray("KHR_draco_mesh_compression"))));
        Assert.Throws<InvalidDataException>(() => FieldSceneAsset.ImportGlb(CreateGlb(), "Ignored"));
    }

    [Fact]
    public void ReorderingSiblingNodesPreservesIdentityAndAnimationTarget()
    {
        FieldSceneAsset first = FieldSceneAsset.ImportGlb(CreateGlb());
        FieldSceneAsset second = FieldSceneAsset.ImportGlb(CreateGlb(root => root["nodes"]![0]!["children"] = new JsonArray(3, 2, 1)));
        Assert.Equal(first.Primitives.Select(primitive => primitive.Id).Order(), second.Primitives.Select(primitive => primitive.Id).Order());
        int cameraNode = second.Cameras.Single(camera => camera.Id == "main").Node;
        Assert.Equal(new Vector3(4, 3, 0), second.SampleTransforms(0, 2)[cameraNode].Translation);
    }

    [Fact]
    public void SamplesWeightedJointForestAndRejectsZeroWeightVertices()
    {
        const int jointEncoding = 0x00000100;
        float packedJoints = BitConverter.Int32BitsToSingle(jointEncoding);
        float[] weights = [packedJoints, packedJoints, packedJoints, 0.25f, 0.75f, 0, 0, 0.25f, 0.75f, 0, 0, 0.25f, 0.75f, 0, 0];
        byte[] source = CreateGlb(AddSkin, weights);
        FieldSceneAsset scene = FieldSceneAsset.ImportGlb(source);
        Assert.Equal(2, scene.Geometry.Bones.Count(bone => bone.IsJoint));
        Assert.True(scene.Geometry.Meshes[0].Skinned);
        Matrix4x4[] transforms = scene.SampleTransforms(0, 2);
        Assert.Equal(new Vector3(4.25f, 3, 0), ModelExchange.PoseWithTransforms(scene.Geometry, transforms)[0][0]);
        Assert.Equal(Vector3.UnitZ, scene.PoseNormals(transforms)[0][0]);

        weights[3] = 0;
        weights[4] = 0;
        InvalidDataException invalid = Assert.Throws<InvalidDataException>(() => FieldSceneAsset.ImportGlb(CreateGlb(AddSkin, weights)));
        Assert.Contains("weights", invalid.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SamplesCubicAndStepClipsIndependentlyAndRejectsSingularCubicScale()
    {
        float[] cubic = [0, 0, 0, 2, 0, 0, 4, 0, 0, 0, 0, 0, 6, 0, 0, 0, 0, 0];
        void AddChannels(JsonObject root)
        {
            root["bufferViews"]!.AsArray().Add(JsonNode.Parse("""{"buffer":0,"byteOffset":68,"byteLength":72}"""));
            root["accessors"]!.AsArray().Add(JsonNode.Parse("""{"bufferView":3,"componentType":5126,"count":6,"type":"VEC3"}"""));
            root["animations"]![0]!["samplers"]![0] = JsonNode.Parse("""{"input":1,"output":3,"interpolation":"CUBICSPLINE"}""");
            root["animations"]!.AsArray().Add(JsonNode.Parse("""
                {"name":"Stepped","channels":[{"sampler":0,"target":{"node":0,"path":"translation"}}],"samplers":[{"input":1,"output":2,"interpolation":"STEP"}]}
                """));
        }
        FieldSceneAsset asset = FieldSceneAsset.ImportGlb(CreateGlb(AddChannels, cubic));
        Assert.Equal(2, asset.Clips.Count);
        Assert.Equal(5, asset.SampleTransforms(0, 2)[0].Translation.X);
        Assert.Equal(2, asset.SampleTransforms(1, 2)[0].Translation.X);

        float[] singular = [0, 0, 0, 1, 1, 1, -4, 0, 0, 4, 0, 0, 1, 1, 1, 0, 0, 0];
        InvalidDataException error = Assert.Throws<InvalidDataException>(() => FieldSceneAsset.ImportGlb(CreateGlb(root =>
        {
            AddChannels(root);
            root["animations"]![0]!["channels"]![0]!["target"]!["path"] = "scale";
        }, singular)));
        Assert.Contains("singular", error.Message);
    }

    private static void AddSkin(JsonObject root)
    {
        root["nodes"]![0]!["children"] = new JsonArray(1, 2, 3, 5, 6);
        root["nodes"]![1]!["skin"] = 0;
        root["nodes"]!.AsArray().Add(JsonNode.Parse("""{"name":"Left","translation":[1,0,0]}"""));
        root["nodes"]!.AsArray().Add(JsonNode.Parse("""{"name":"Right","translation":[0,4,0]}"""));
        root["skins"] = JsonNode.Parse("""[{"joints":[5,6]}]""");

        // The second static node needs its own primitive without skin attributes.
        root["meshes"]!.AsArray().Add(root["meshes"]![0]!.DeepClone());
        root["nodes"]![3]!["mesh"] = 2;
        JsonNode attributes = root["meshes"]![0]!["primitives"]![0]!["attributes"]!;
        attributes["JOINTS_0"] = 3;
        attributes["WEIGHTS_0"] = 4;
        root["bufferViews"]!.AsArray().Add(JsonNode.Parse("""{"buffer":0,"byteOffset":68,"byteLength":12}"""));
        root["bufferViews"]!.AsArray().Add(JsonNode.Parse("""{"buffer":0,"byteOffset":80,"byteLength":48}"""));
        root["accessors"]!.AsArray().Add(JsonNode.Parse("""{"bufferView":3,"componentType":5121,"count":3,"type":"VEC4"}"""));
        root["accessors"]!.AsArray().Add(JsonNode.Parse("""{"bufferView":4,"componentType":5126,"count":3,"type":"VEC4"}"""));
    }

    private static byte[] CreateGlb(Action<JsonObject>? edit = null, float[]? extraValues = null)
    {
        float[] geometry = [0, 0, 0, 1, 0, 0, 0, 1, 0, 1, 3, 2, 0, 0, 6, 0, 0, .. extraValues ?? []];
        byte[] bytes = new byte[geometry.Length * sizeof(float)];
        for (int i = 0; i < geometry.Length; i++)
        {
            BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(i * sizeof(float)), geometry[i]);
        }
        JsonObject root = JsonNode.Parse("""
            {
              "asset": {"version": "2.0"},
              "scene": 0,
              "scenes": [{"name":"Room","nodes":[0],"extras":{"memoria_scene_id":"room"}},{"name":"Ignored","nodes":[4]}],
              "nodes": [
                {"name":"Root","children":[1,2,3],"translation":[2,0,0]},
                {"name":"window","mesh":0,"camera":0,"translation":[0,3,0]},
                {"name":"Map","camera":1},
                {"name":"desk","mesh":0},
                {"name":"OffSceneMesh","mesh":1}
              ],
              "meshes": [
                {"primitives":[{"attributes":{"POSITION":0},"material":0}]},
                {"primitives":[{"attributes":{"POSITION":0},"material":1}]}
              ],
              "materials": [
                {"name":"Glass","extras":{"memoria_scene_id":"glass"},"alphaMode":"BLEND","doubleSided":true,
                 "extensions":{"KHR_materials_unlit":{}},"pbrMetallicRoughness":{"baseColorFactor":[0.2,0.3,0.4,0.6]}},
                {"name":"External","pbrMetallicRoughness":{"baseColorTexture":{"index":0}}}
              ],
              "extensionsRequired":["KHR_materials_unlit"],
              "textures":[{"source":0}],
              "images":[{"uri":"missing.png"}],
              "cameras":[
                {"name":"Camera","type":"perspective","perspective":{"yfov":0.6,"znear":0.01,"aspectRatio":1.7777778},
                 "extras":{"memoria_scene_id":"main","memoria_projection":{"principalPoint":[0.4,0.6]}}},
                {"name":"Map","type":"orthographic","orthographic":{"xmag":4,"ymag":2,"znear":0,"zfar":20}}
              ],
              "animations":[
                {"name":"Opening","extras":{"memoria_scene_id":"opening"},
                 "channels":[{"sampler":0,"target":{"node":0,"path":"translation"}}],"samplers":[{"input":1,"output":2}]},
                {"name":"Unused","channels":[{"sampler":0,"target":{"node":4,"path":"weights"}}],"samplers":[{"input":1,"output":2}]}
              ],
              "buffers": [{"byteLength":68}],
              "bufferViews":[{"buffer":0,"byteLength":36},{"buffer":0,"byteOffset":36,"byteLength":8},{"buffer":0,"byteOffset":44,"byteLength":24}],
              "accessors":[{"bufferView":0,"componentType":5126,"count":3,"type":"VEC3"},
                           {"bufferView":1,"componentType":5126,"count":2,"type":"SCALAR"},
                           {"bufferView":2,"componentType":5126,"count":2,"type":"VEC3"}]
            }
            """)!.AsObject();
        root["buffers"]![0]!["byteLength"] = bytes.Length;
        edit?.Invoke(root);
        byte[] json = Encoding.UTF8.GetBytes(root.ToJsonString());
        int jsonLength = (json.Length + 3) & ~3;
        byte[] glb = new byte[28 + jsonLength + bytes.Length];
        BinaryPrimitives.WriteUInt32LittleEndian(glb, 0x46546c67);
        BinaryPrimitives.WriteInt32LittleEndian(glb.AsSpan(4), 2);
        BinaryPrimitives.WriteInt32LittleEndian(glb.AsSpan(8), glb.Length);
        BinaryPrimitives.WriteInt32LittleEndian(glb.AsSpan(12), jsonLength);
        BinaryPrimitives.WriteUInt32LittleEndian(glb.AsSpan(16), 0x4e4f534a);
        glb.AsSpan(20, jsonLength).Fill((byte)' ');
        json.CopyTo(glb.AsSpan(20));
        int binaryOffset = 20 + jsonLength;
        BinaryPrimitives.WriteInt32LittleEndian(glb.AsSpan(binaryOffset), bytes.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(glb.AsSpan(binaryOffset + 4), 0x004e4942);
        bytes.CopyTo(glb.AsSpan(binaryOffset + 8));
        return glb;
    }
}
