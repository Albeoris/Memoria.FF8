using System.Numerics;
using System.Text.Json;
using Albeoris.Games.FF8.Imaging;

namespace Memoria.FF8.Assets;

public static partial class ModelExchange
{
    internal static FieldSceneAsset ImportFieldSceneGlb(ReadOnlySpan<byte> content, string? sceneName)
    {
        (byte[] json, byte[] binary) = ReadGlbChunks(content);
        try
        {
            using JsonDocument document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 128 });
            return new GlbReader(document.RootElement, binary).ReadFieldScene(sceneName);
        }
        catch (Exception exception) when (IsGlbStructureException(exception))
        {
            throw new InvalidDataException("The field scene GLB contains invalid or unsupported structural data.", exception);
        }
    }

    internal static Matrix4x4[] SampleFieldTransforms(EditableModel model, int animationIndex, float seconds)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(animationIndex, -1);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(animationIndex, model.Animations.Count);
        if (!float.IsFinite(seconds))
        {
            throw new ArgumentOutOfRangeException(nameof(seconds));
        }
        return SampleTransforms(model, animationIndex, seconds);
    }

    private sealed partial class GlbReader
    {
        private const int SceneVertexLimit = 1_000_000;
        private const int SceneIndexLimit = 3_000_000;
        private const int SceneSampleLimit = 1_000_000;
        private const string UnlitExtension = "KHR_materials_unlit";
        private const string IdentityProperty = "memoria_scene_id";
        private bool _readingFieldScene;
        private long _sceneAccessorBytes;

        public FieldSceneAsset ReadFieldScene(string? requestedScene)
        {
            _readingFieldScene = true;
            ValidateSceneHeader();
            JsonElement scene = SelectScene(requestedScene);
            string name = String(scene, "name", "Scene");
            string sceneId = SceneId(scene, "scene/" + Segment(name));
            JsonElement nodes = Root.GetProperty("nodes");
            (List<(int Index, int Parent, string Id)> reachable, Dictionary<int, int> nodeMap) = ReadSceneHierarchy(scene);
            (Dictionary<int, (int[] Joints, IReadOnlyDictionary<int, Matrix4x4> Binds)> skins, HashSet<int> joints) = ReadSceneSkins(nodes, reachable, nodeMap);

            EditableBone[] bones = new EditableBone[reachable.Count];
            for (int nodeIndex = 0; nodeIndex < bones.Length; nodeIndex++)
            {
                (int index, int parent, string id) = reachable[nodeIndex];
                JsonElement node = nodes[index];
                if (node.TryGetProperty("rotation", out JsonElement rotationValue) && rotationValue.GetArrayLength() != 4)
                {
                    throw new InvalidDataException($"Field scene node '{id}' rotation must contain four components.");
                }
                (Vector3 translation, Quaternion rotation, Vector3 scale) = ReadNodeTransform(node);
                if (scale.X == 0 || scale.Y == 0 || scale.Z == 0)
                {
                    throw new InvalidDataException($"Field scene node '{id}' has a singular transform.");
                }
                bones[nodeIndex] = new EditableBone(String(node, "name", "(unnamed)"), parent, translation, rotation, scale, joints.Contains(nodeIndex)) { StableId = id };
            }
            Validate(new EditableModel(name, "field-scene", bones, [], [], [], default));
            Matrix4x4[] globals = Globals(bones);
            foreach (Matrix4x4 matrix in globals)
            {
                if (!MatrixValues(matrix).All(float.IsFinite) || !Matrix4x4.Invert(matrix, out Matrix4x4 inverse) || !MatrixValues(inverse).All(float.IsFinite))
                {
                    throw new InvalidDataException("A field scene global transform overflows or is singular.");
                }
            }

            List<PixelImage> images = [];
            Dictionary<int, int> imageMap = [];
            List<FieldSceneMaterial> materials = [];
            HashSet<string> materialIds = new(StringComparer.Ordinal);
            Dictionary<int, int> materialMap = [];
            List<FieldSceneCamera> cameras = [];
            HashSet<string> cameraIds = new(StringComparer.Ordinal);
            List<FieldScenePrimitive> primitives = [];
            HashSet<string> primitiveIds = new(StringComparer.Ordinal);
            List<EditableMesh> meshes = [];
            int totalVertices = 0;
            int totalIndices = 0;

            for (int nodeIndex = 0; nodeIndex < reachable.Count; nodeIndex++)
            {
                (int index, _, string nodeId) = reachable[nodeIndex];
                JsonElement node = nodes[index];
                if (node.TryGetProperty("camera", out JsonElement cameraRef))
                {
                    FieldSceneCamera camera = ReadCamera(At("cameras", cameraRef.GetInt32()), node, nodeId, nodeIndex);
                    if (!cameraIds.Add(camera.Id))
                    {
                        throw AmbiguousId("camera", camera.Id);
                    }
                    cameras.Add(camera);
                }
                if (!node.TryGetProperty("mesh", out JsonElement meshRef))
                {
                    continue;
                }
                JsonElement mesh = At("meshes", meshRef.GetInt32());
                JsonElement meshPrimitives = mesh.GetProperty("primitives");
                int[] skinJoints = [];
                IReadOnlyDictionary<int, Matrix4x4>? binds = null;
                if (node.TryGetProperty("skin", out JsonElement jointRef))
                {
                    (skinJoints, binds) = skins[jointRef.GetInt32()];
                }
                foreach (JsonElement primitive in meshPrimitives.EnumerateArray())
                {
                    if (meshes.Count == 4096 || Int(primitive, "mode", 4) != 4 || primitive.TryGetProperty("targets", out _))
                    {
                        throw new InvalidDataException("Export field scene triangle primitives without morph targets, within the 4096 primitive limit.");
                    }
                    WarnExtensions(primitive, $"Mesh node '{nodeId}'");
                    int sourceMaterial = -1;
                    if (primitive.TryGetProperty("material", out JsonElement materialRef) && (sourceMaterial = materialRef.GetInt32()) < 0)
                    {
                        throw new InvalidDataException("A field scene material reference is out of range.");
                    }
                    if (!materialMap.TryGetValue(sourceMaterial, out int materialIndex))
                    {
                        FieldSceneMaterial imported = ReadSceneMaterial(sourceMaterial, images, imageMap);
                        if (!materialIds.Add(imported.Id))
                        {
                            throw AmbiguousId("material", imported.Id);
                        }
                        materialIndex = materials.Count;
                        materialMap.Add(sourceMaterial, materialIndex);
                        materials.Add(imported);
                    }
                    FieldSceneMaterial material = materials[materialIndex];
                    string fallback = meshPrimitives.GetArrayLength() == 1 ? "geometry" : material.Id;
                    string primitiveId = $"{nodeId}/primitive/{SceneId(primitive, fallback)}";
                    if (!primitiveIds.Add(primitiveId))
                    {
                        throw AmbiguousId("primitive", primitiveId);
                    }
                    JsonElement attributes = primitive.GetProperty("attributes");
                    CheckSceneVertexEncodings(attributes, material.Texture >= 0, skinJoints.Length != 0);
                    EditableVertex[] vertices = ReadVertices(attributes, skinJoints, ref totalVertices, Vector4.One, SceneVertexLimit);
                    int[] indices = ReadTriangleIndices(primitive, vertices.Length, SceneIndexLimit - totalIndices);
                    totalIndices += indices.Length;
                    foreach (int vertexIndex in indices)
                    {
                        if ((uint)vertexIndex >= vertices.Length)
                        {
                            throw new InvalidDataException($"Field scene primitive '{primitiveId}' has an out-of-range triangle index.");
                        }
                    }
                    if (!attributes.TryGetProperty("NORMAL", out _))
                    {
                        GenerateNormals(vertices, indices);
                    }
                    meshes.Add(new EditableMesh(String(mesh, "name", bones[nodeIndex].Name), Array.AsReadOnly(vertices), Array.AsReadOnly(indices), material.Texture, globals[nodeIndex])
                    {
                        Node = skinJoints.Length == 0 ? nodeIndex : -1,
                        Skinned = skinJoints.Length != 0,
                        InverseBindMatrices = binds,
                        Transparent = material.AlphaMode == FieldSceneAlphaMode.Blend,
                        AlphaCutoff = material.AlphaMode == FieldSceneAlphaMode.Mask ? material.AlphaCutoff : null,
                        DoubleSided = material.DoubleSided
                    });
                    primitives.Add(new FieldScenePrimitive(primitiveId, nodeId, materialIndex));
                }
            }
            if (meshes.Count == 0)
            {
                throw new InvalidDataException("The selected field scene contains no triangle geometry.");
            }
            (EditableAnimation[] animations, FieldSceneClip[] clips) = ReadSceneClips(nodeMap, nodes);
            EditableModel geometry = new(name, "field-scene", Array.AsReadOnly(bones), meshes.AsReadOnly(), images.AsReadOnly(), Array.AsReadOnly(animations), default);
            Validate(geometry);
            return new FieldSceneAsset(sceneId, geometry, primitives.ToArray(), materials.ToArray(), cameras.ToArray(), clips, _warnings.Distinct().ToArray());
        }

        private (List<(int Index, int Parent, string Id)>, Dictionary<int, int>) ReadSceneHierarchy(JsonElement scene)
        {
            List<(int Index, int Parent, string Id)> reachable = [];
            Dictionary<int, int> map = [];
            HashSet<string> ids = new(StringComparer.Ordinal);
            void Visit(int index, int parent, string path, int depth)
            {
                if (depth > 128 || map.ContainsKey(index))
                {
                    throw new InvalidDataException("The selected field scene has a cyclic, multiply parented or excessively deep node hierarchy.");
                }
                JsonElement node = At("nodes", index);
                string id = SceneId(node, path + "/" + Segment(String(node, "name", "(unnamed)")));
                if (!ids.Add(id))
                {
                    throw AmbiguousId("node", id);
                }
                WarnExtensions(node, $"Node '{id}'");
                int current = reachable.Count;
                map.Add(index, current);
                reachable.Add((index, parent, id));
                foreach (JsonElement child in OptionalArray(node, "children"))
                {
                    Visit(child.GetInt32(), current, id, depth + 1);
                }
            }
            foreach (JsonElement node in scene.GetProperty("nodes").EnumerateArray())
            {
                Visit(node.GetInt32(), -1, "node", 0);
            }
            return (reachable, map);
        }

        private (Dictionary<int, (int[], IReadOnlyDictionary<int, Matrix4x4>)>, HashSet<int>) ReadSceneSkins(JsonElement nodes, List<(int Index, int Parent, string Id)> reachable, Dictionary<int, int> nodeMap)
        {
            Dictionary<int, (int[], IReadOnlyDictionary<int, Matrix4x4>)> skins = [];
            HashSet<int> joints = [];
            foreach ((int index, _, _) in reachable)
            {
                if (!nodes[index].TryGetProperty("skin", out JsonElement reference) || skins.ContainsKey(reference.GetInt32()))
                {
                    continue;
                }
                int skinIndex = reference.GetInt32();
                JsonElement skin = At("skins", skinIndex);
                JsonElement skinJoints = skin.GetProperty("joints");
                if (skinJoints.GetArrayLength() is < 1 or > 512)
                {
                    throw new InvalidDataException("Each field scene skin must contain between 1 and 512 joints.");
                }
                if (skin.TryGetProperty("skeleton", out JsonElement skeleton) && !nodeMap.ContainsKey(skeleton.GetInt32()))
                {
                    throw new InvalidDataException("A field scene skin skeleton is outside the selected scene.");
                }
                int[] mapped = new int[skinJoints.GetArrayLength()];
                HashSet<int> distinct = [];
                for (int i = 0; i < mapped.Length; i++)
                {
                    if (!nodeMap.TryGetValue(skinJoints[i].GetInt32(), out int joint) || !distinct.Add(joint))
                    {
                        throw new InvalidDataException("A field scene skin references a duplicate joint or a joint outside the selected scene.");
                    }
                    mapped[i] = joint;
                    joints.Add(joint);
                }
                float[][]? matrices = null;
                if (skin.TryGetProperty("inverseBindMatrices", out JsonElement inverseBind))
                {
                    int accessor = inverseBind.GetInt32();
                    CheckCount(accessor, mapped.Length, "Inverse bind matrix count does not match the field scene skin joints.");
                    matrices = Accessor(accessor, "MAT4", 5126);
                }
                Dictionary<int, Matrix4x4> binds = new(mapped.Length);
                for (int i = 0; i < mapped.Length; i++)
                {
                    Matrix4x4 bind = matrices is null ? Matrix4x4.Identity : Matrix(matrices[i]);
                    if (bind.M14 != 0 || bind.M24 != 0 || bind.M34 != 0 || bind.M44 != 1 || !Matrix4x4.Invert(bind, out Matrix4x4 inverse) || !MatrixValues(inverse).All(float.IsFinite))
                    {
                        throw new InvalidDataException("A field scene inverse bind matrix is invalid or singular.");
                    }
                    binds.Add(mapped[i], bind);
                }
                skins.Add(skinIndex, (mapped, new System.Collections.ObjectModel.ReadOnlyDictionary<int, Matrix4x4>(binds)));
            }
            return (skins, joints);
        }

        private void ValidateSceneHeader()
        {
            JsonElement asset = Root.GetProperty("asset");
            if (String(asset, "version", "") != "2.0" || String(asset, "minVersion", "2.0") != "2.0")
            {
                throw new InvalidDataException("Field scenes require glTF 2.0.");
            }
            foreach (JsonElement extension in OptionalArray(Root, "extensionsRequired"))
            {
                if (extension.GetString() != UnlitExtension)
                {
                    throw new InvalidDataException($"Required glTF extension '{extension.GetString()}' is unsupported. Export an uncompressed GLB with baked UV0 and standard materials.");
                }
            }
            JsonElement buffers = Root.GetProperty("buffers");
            if (buffers.GetArrayLength() != 1 || buffers[0].TryGetProperty("uri", out _) || Int(buffers[0], "byteLength") < 1 || BinaryLength - Int(buffers[0], "byteLength") is < 0 or > 3)
            {
                throw new InvalidDataException("Field scenes require one self-contained binary GLB buffer.");
            }
            foreach ((string key, int limit) in new (string, int)[]
            {
                ("scenes", 64), ("nodes", 4096), ("meshes", 4096), ("accessors", 16384), ("bufferViews", 16384),
                ("materials", 1024), ("textures", 1024), ("images", 1024), ("samplers", 1024), ("skins", 512), ("cameras", 256), ("animations", 512)
            })
            {
                if (Root.TryGetProperty(key, out JsonElement array) && array.GetArrayLength() > limit)
                {
                    throw new InvalidDataException($"The field scene exceeds the {limit} {key} limit.");
                }
            }
            WarnExtensions(Root, "Field scene");
        }

        private JsonElement SelectScene(string? requestedScene)
        {
            JsonElement scenes = Root.GetProperty("scenes");
            if (requestedScene is not null)
            {
                JsonElement[] matches = scenes.EnumerateArray().Where(scene => requestedScene == String(scene, "name", "") || requestedScene == ReadMetadataString(scene, IdentityProperty, null)).ToArray();
                return matches.Length == 1 ? matches[0] : throw new InvalidDataException($"Field scene '{requestedScene}' is missing or ambiguous.");
            }
            if (Root.TryGetProperty("scene", out JsonElement declared))
            {
                return At("scenes", declared.GetInt32());
            }
            return scenes.GetArrayLength() == 1 ? scenes[0] : throw new InvalidDataException("Choose a named scene or specify a default glTF scene before export.");
        }

        private FieldSceneMaterial ReadSceneMaterial(int sourceMaterial, List<PixelImage> images, Dictionary<int, int> imageMap)
        {
            FieldSceneSampler sampler = new(10497, 10497, 9987, 9729);
            if (sourceMaterial == -1)
            {
                return new FieldSceneMaterial("material/default", "Default", Vector4.One, -1, FieldSceneAlphaMode.Opaque, 0.5f, false, false, sampler);
            }
            JsonElement material = At("materials", sourceMaterial);
            string name = String(material, "name", "(unnamed)");
            string id = SceneId(material, "material/" + Segment(name));
            WarnExtensions(material, $"Material '{id}'", UnlitExtension);
            bool unlit = material.TryGetProperty("extensions", out JsonElement extensions) && extensions.TryGetProperty(UnlitExtension, out _);
            FieldSceneAlphaMode alpha = String(material, "alphaMode", "OPAQUE") switch
            {
                "OPAQUE" => FieldSceneAlphaMode.Opaque,
                "MASK" => FieldSceneAlphaMode.Mask,
                "BLEND" => FieldSceneAlphaMode.Blend,
                _ => throw new InvalidDataException($"Material '{id}' has an unsupported alpha mode.")
            };
            float cutoff = Range01(Float(material, "alphaCutoff", 0.5f), $"Material '{id}' alpha cutoff");
            bool doubleSided = material.TryGetProperty("doubleSided", out JsonElement sided) && sided.GetBoolean();
            Vector4 factor = Vector4.One;
            float metallic = 1;
            float roughness = 1;
            int texture = -1;
            if (material.TryGetProperty("pbrMetallicRoughness", out JsonElement pbr))
            {
                if (pbr.TryGetProperty("baseColorFactor", out JsonElement color) && color.GetArrayLength() != 4)
                {
                    throw new InvalidDataException($"Material '{id}' base color factor needs four components.");
                }
                factor = Vec4(pbr, "baseColorFactor", Vector4.One);
                for (int i = 0; i < 4; i++)
                {
                    Range01(factor[i], $"Material '{id}' base color factor");
                }
                metallic = Range01(Float(pbr, "metallicFactor", 1), $"Material '{id}' metallic factor");
                roughness = Range01(Float(pbr, "roughnessFactor", 1), $"Material '{id}' roughness factor");
                if (pbr.TryGetProperty("baseColorTexture", out JsonElement info))
                {
                    (texture, sampler) = ReadSceneTexture(info, id, images, imageMap, sampler);
                }
            }
            if (!unlit && (material.TryGetProperty("normalTexture", out _) || material.TryGetProperty("emissiveTexture", out _) || material.TryGetProperty("occlusionTexture", out _) || Vec3(material, "emissiveFactor", Vector3.Zero) != Vector3.Zero || pbr.ValueKind == JsonValueKind.Object && pbr.TryGetProperty("metallicRoughnessTexture", out _)))
            {
                _warnings.Add($"Material '{id}' uses unsupported PBR maps or emission. Bake the desired appearance into base color for the field renderer.");
            }
            return new FieldSceneMaterial(id, name, factor, texture, alpha, cutoff, doubleSided, unlit, sampler) { MetallicFactor = metallic, RoughnessFactor = roughness };
        }

        private (int, FieldSceneSampler) ReadSceneTexture(JsonElement info, string id, List<PixelImage> images, Dictionary<int, int> imageMap, FieldSceneSampler sampler)
        {
            if (Int(info, "texCoord", 0) != 0 || info.TryGetProperty("extensions", out JsonElement textureExtensions) && textureExtensions.EnumerateObject().Any())
            {
                throw new InvalidDataException($"Material '{id}' must use UV0 with texture-coordinate transforms baked into the UVs.");
            }
            JsonElement definition = At("textures", Int(info, "index"));
            WarnExtensions(definition, $"Material '{id}' base-color texture");
            int source = Int(definition, "source");
            if (!imageMap.TryGetValue(source, out int texture))
            {
                texture = images.Count;
                images.Add(ReadImage(source));
                imageMap.Add(source, texture);
            }
            if (definition.TryGetProperty("sampler", out JsonElement reference))
            {
                JsonElement sampling = At("samplers", reference.GetInt32());
                sampler = new FieldSceneSampler(Int(sampling, "wrapS", sampler.WrapS), Int(sampling, "wrapT", sampler.WrapT), Int(sampling, "minFilter", sampler.MinFilter), Int(sampling, "magFilter", sampler.MagFilter));
                if (sampler.WrapS is not (33071 or 33648 or 10497) || sampler.WrapT is not (33071 or 33648 or 10497) || sampler.MinFilter is not (9728 or 9729 or 9984 or 9985 or 9986 or 9987) || sampler.MagFilter is not (9728 or 9729))
                {
                    throw new InvalidDataException($"Material '{id}' has unsupported sampler values.");
                }
                if (sampling.TryGetProperty("minFilter", out _) && sampler.MinFilter is >= 9984 and <= 9987)
                {
                    _warnings.Add($"Material '{id}' requests mipmapped minification. The field renderer uses the base level. Export NEAREST or LINEAR minification to select the supported filter explicitly.");
                }
            }
            return (texture, sampler);
        }

        private FieldSceneCamera ReadCamera(JsonElement camera, JsonElement node, string nodeId, int nodeIndex)
        {
            string name = String(camera, "name", String(node, "name", "Camera"));
            string id = ReadMetadataString(node, "memoria_scene_camera_id", null) ?? SceneId(camera, nodeId);
            CheckId(id);
            WarnExtensions(camera, $"Camera '{id}'");
            Vector2? principalPoint = null;
            if (camera.TryGetProperty("extras", out JsonElement extras) && extras.TryGetProperty("memoria_projection", out JsonElement projection) && projection.TryGetProperty("principalPoint", out JsonElement point))
            {
                if (point.GetArrayLength() != 2 || !float.IsFinite(point[0].GetSingle()) || !float.IsFinite(point[1].GetSingle()))
                {
                    throw new InvalidDataException($"Camera '{id}' has an invalid principal point.");
                }
                principalPoint = new Vector2(point[0].GetSingle(), point[1].GetSingle());
            }
            switch (String(camera, "type", ""))
            {
                case "perspective":
                {
                    JsonElement data = camera.GetProperty("perspective");
                    float fov = data.GetProperty("yfov").GetSingle();
                    float near = data.GetProperty("znear").GetSingle();
                    float? far = OptionalFloat(data, "zfar");
                    float? aspect = OptionalFloat(data, "aspectRatio");
                    if (!float.IsFinite(fov) || fov <= 0 || fov >= MathF.PI || !float.IsFinite(near) || near <= 0 || far is float farPlane && (!float.IsFinite(farPlane) || farPlane <= near) || aspect is float ratio && (!float.IsFinite(ratio) || ratio <= 0))
                    {
                        throw new InvalidDataException($"Camera '{id}' has invalid perspective intrinsics.");
                    }
                    return new FieldSceneCamera(id, name, nodeIndex, false, fov, aspect, near, far, 0, 0) { PrincipalPoint = principalPoint };
                }
                case "orthographic":
                {
                    JsonElement data = camera.GetProperty("orthographic");
                    float xmag = data.GetProperty("xmag").GetSingle();
                    float ymag = data.GetProperty("ymag").GetSingle();
                    float near = data.GetProperty("znear").GetSingle();
                    float far = data.GetProperty("zfar").GetSingle();
                    float aspect = xmag / ymag;
                    if (!float.IsFinite(xmag) || xmag <= 0 || !float.IsFinite(ymag) || ymag <= 0 || !float.IsFinite(aspect) || !float.IsFinite(near) || near < 0 || !float.IsFinite(far) || far <= near)
                    {
                        throw new InvalidDataException($"Camera '{id}' has invalid orthographic intrinsics.");
                    }
                    return new FieldSceneCamera(id, name, nodeIndex, true, 0, aspect, near, far, xmag, ymag) { PrincipalPoint = principalPoint };
                }
                default:
                    throw new InvalidDataException($"Camera '{id}' has an unsupported projection.");
            }
        }

        private (EditableAnimation[], FieldSceneClip[]) ReadSceneClips(Dictionary<int, int> nodeMap, JsonElement nodes)
        {
            List<EditableAnimation> animations = [];
            List<FieldSceneClip> clips = [];
            HashSet<string> ids = new(StringComparer.Ordinal);
            int totalSamples = 0;
            foreach (JsonElement animation in OptionalArray(Root, "animations"))
            {
                string name = String(animation, "name", "(unnamed)");
                List<EditableAnimationChannel> channels = [];
                float start = float.PositiveInfinity;
                float end = 0;
                JsonElement samplers = animation.GetProperty("samplers");
                JsonElement sourceChannels = animation.GetProperty("channels");
                if (sourceChannels.GetArrayLength() > 12288 || samplers.GetArrayLength() > 12288)
                {
                    throw new InvalidDataException($"Animation '{name}' exceeds the field scene channel/sampler limit.");
                }
                foreach (JsonElement channel in sourceChannels.EnumerateArray())
                {
                    JsonElement target = channel.GetProperty("target");
                    if (!target.TryGetProperty("node", out JsonElement reference))
                    {
                        throw new InvalidDataException($"Animation '{name}' has a non-node target. Export node TRS channels.");
                    }
                    int sourceNode = reference.GetInt32();
                    if ((uint)sourceNode >= nodes.GetArrayLength())
                    {
                        throw new InvalidDataException($"Animation '{name}' references a node outside the GLB.");
                    }
                    if (!nodeMap.TryGetValue(sourceNode, out int node))
                    {
                        continue;
                    }
                    string path = String(target, "path", "");
                    if (path is not ("translation" or "rotation" or "scale") || nodes[sourceNode].TryGetProperty("matrix", out _))
                    {
                        throw new InvalidDataException($"Animation '{name}' has an unsupported target. Export node translation, rotation and scale (TRS) directly.");
                    }
                    int samplerIndex = Int(channel, "sampler");
                    if ((uint)samplerIndex >= samplers.GetArrayLength())
                    {
                        throw new InvalidDataException($"Animation '{name}' references a nonexistent sampler.");
                    }
                    JsonElement sampler = samplers[samplerIndex];
                    string interpolation = String(sampler, "interpolation", "LINEAR");
                    if (interpolation is not ("STEP" or "LINEAR" or "CUBICSPLINE"))
                    {
                        throw new InvalidDataException($"Animation '{name}' has unsupported interpolation '{interpolation}'.");
                    }
                    int input = Int(sampler, "input");
                    int output = Int(sampler, "output");
                    int count = Int(At("accessors", input), "count");
                    int outputCount = checked(count * (interpolation == "CUBICSPLINE" ? 3 : 1));
                    totalSamples = checked(totalSamples + count + outputCount);
                    if (count < 1 || totalSamples > SceneSampleLimit)
                    {
                        throw new InvalidDataException("The field scene exceeds the supported animation sample limit.");
                    }
                    CheckCount(output, outputCount, $"Animation '{name}' times and values have inconsistent counts.");
                    float[] times = Accessor(input, "SCALAR", 5126).Select(value => value[0]).ToArray();
                    Vector4[] values = Accessor(output, path == "rotation" ? "VEC4" : "VEC3", 5126).Select(value => Vector(value, 0)).ToArray();
                    if (path == "scale")
                    {
                        CheckSceneScale(name, times, values, interpolation);
                    }
                    start = Math.Min(start, times[0]);
                    end = Math.Max(end, times[^1]);
                    channels.Add(new EditableAnimationChannel(node, path, times, values, interpolation));
                }
                if (channels.Count == 0)
                {
                    continue;
                }
                string id = SceneId(animation, "clip/" + Segment(name));
                if (!ids.Add(id))
                {
                    throw AmbiguousId("clip", id);
                }
                clips.Add(new FieldSceneClip(id, name, animations.Count, start, end));
                animations.Add(new EditableAnimation(name, channels.AsReadOnly()));
            }
            return (animations.ToArray(), clips.ToArray());
        }

        private static void CheckSceneScale(string clip, float[] times, Vector4[] values, string interpolation)
        {
            bool cubic = interpolation == "CUBICSPLINE";
            for (int i = 0; i < times.Length; i++)
            {
                int key = cubic ? 3 * i + 1 : i;
                for (int axis = 0; axis < 3; axis++)
                {
                    float current = values[key][axis];
                    if (current == 0)
                    {
                        throw new InvalidDataException($"Animation '{clip}' animates a field scene scale to zero.");
                    }
                    if (i == 0 || interpolation == "STEP")
                    {
                        continue;
                    }
                    float previous = values[key - (cubic ? 3 : 1)][axis];
                    if (MathF.Sign(previous) != MathF.Sign(current))
                    {
                        throw new InvalidDataException($"Animation '{clip}' interpolates through a singular scale. Use a STEP channel for discrete mirroring.");
                    }
                    if (cubic)
                    {
                        double interval = (double)times[i] - times[i - 1];
                        double startTangent = interval * values[key - 2][axis];
                        double endTangent = interval * values[key - 1][axis];
                        CheckCubicScale(clip, previous, current, startTangent, endTangent);
                    }
                }
            }
        }

        private static void CheckCubicScale(string clip, double start, double end, double startTangent, double endTangent)
        {
            double cubic = 2 * (start - end) + startTangent + endTangent;
            double quadratic = 3 * (end - start) - 2 * startTangent - endTangent;
            void CheckExtremum(double parameter)
            {
                if (parameter <= 0 || parameter >= 1)
                {
                    return;
                }
                double scale = ((cubic * parameter + quadratic) * parameter + startTangent) * parameter + start;
                if (scale == 0 || Math.Sign(scale) != Math.Sign(start) || Math.Abs(scale) > float.MaxValue)
                {
                    throw new InvalidDataException($"Animation '{clip}' has a singular or nonfinite cubic scale between keys.");
                }
            }
            if (cubic == 0)
            {
                if (quadratic != 0)
                {
                    CheckExtremum(-startTangent / (2 * quadratic));
                }
                return;
            }
            double discriminant = quadratic * quadratic - 3 * cubic * startTangent;
            if (discriminant >= 0)
            {
                double root = Math.Sqrt(discriminant);
                CheckExtremum((-quadratic + root) / (3 * cubic));
                CheckExtremum((-quadratic - root) / (3 * cubic));
            }
        }

        private void CheckSceneVertexEncodings(JsonElement attributes, bool textured, bool skinned)
        {
            int count = Int(At("accessors", Int(attributes, "POSITION")), "count");
            foreach (JsonProperty attribute in attributes.EnumerateObject())
            {
                string name = attribute.Name;
                if ((name.StartsWith("JOINTS_", StringComparison.Ordinal) && name != "JOINTS_0") || (name.StartsWith("WEIGHTS_", StringComparison.Ordinal) && name != "WEIGHTS_0"))
                {
                    throw new InvalidDataException("A field scene vertex supports at most four skin influences. Reduce the influences before export.");
                }
                if (name is not ("NORMAL" or "TEXCOORD_0" or "COLOR_0" or "JOINTS_0" or "WEIGHTS_0"))
                {
                    continue;
                }
                if (!skinned && name is ("JOINTS_0" or "WEIGHTS_0"))
                {
                    throw new InvalidDataException("Skin vertex attributes require a skin on the field scene mesh node.");
                }
                int index = attribute.Value.GetInt32();
                CheckCount(index, count, "Field scene vertex attributes have inconsistent counts.");
                JsonElement accessor = At("accessors", index);
                if (name == "COLOR_0" && String(accessor, "type", "") is not ("VEC3" or "VEC4"))
                {
                    throw new InvalidDataException("Field scene vertex color must use RGB or RGBA.");
                }
                if (name is "TEXCOORD_0" or "COLOR_0" or "WEIGHTS_0")
                {
                    int type = Int(accessor, "componentType");
                    bool normalized = accessor.TryGetProperty("normalized", out JsonElement normal) && normal.GetBoolean();
                    if (type != 5126 && (type is not (5121 or 5123) || !normalized))
                    {
                        throw new InvalidDataException($"{name} must use floating-point or normalized unsigned components.");
                    }
                }
            }
            if (textured && !attributes.TryGetProperty("TEXCOORD_0", out _))
            {
                throw new InvalidDataException("A textured field scene primitive requires UV0.");
            }
        }

        private void CheckCount(int accessor, int count, string message)
        {
            if (Int(At("accessors", accessor), "count") != count)
            {
                throw new InvalidDataException(message);
            }
        }

        private static void GenerateNormals(EditableVertex[] vertices, int[] indices)
        {
            Vector3[] normals = new Vector3[vertices.Length];
            for (int i = 0; i < indices.Length; i += 3)
            {
                int a = indices[i];
                int b = indices[i + 1];
                int c = indices[i + 2];
                Vector3 cross = Vector3.Cross(vertices[b].Position - vertices[a].Position, vertices[c].Position - vertices[a].Position);
                normals[a] += cross;
                normals[b] += cross;
                normals[c] += cross;
            }
            for (int i = 0; i < vertices.Length; i++)
            {
                float length = normals[i].LengthSquared();
                Vector3 normal = float.IsFinite(length) && length > 0 ? normals[i] / MathF.Sqrt(length) : Vector3.Zero;
                vertices[i] = vertices[i] with { Normal = normal };
            }
        }

        private void WarnExtensions(JsonElement owner, string description, string? known = null)
        {
            if (!owner.TryGetProperty("extensions", out JsonElement extensions))
            {
                return;
            }
            foreach (JsonProperty extension in extensions.EnumerateObject())
            {
                if (extension.Name != known)
                {
                    _warnings.Add($"{description} uses unsupported optional extension '{extension.Name}'. Bake its visual effects before export.");
                }
            }
        }

        private static IEnumerable<JsonElement> OptionalArray(JsonElement owner, string property)
        {
            return owner.TryGetProperty(property, out JsonElement element) ? element.EnumerateArray() : Enumerable.Empty<JsonElement>();
        }

        private static string SceneId(JsonElement owner, string fallback)
        {
            string id = ReadMetadataString(owner, IdentityProperty, null) ?? fallback;
            CheckId(id);
            return id;
        }

        private static void CheckId(string id)
        {
            if (string.IsNullOrWhiteSpace(id) || id.Length > 1024 || id.Any(char.IsControl))
            {
                throw new InvalidDataException("Field scene identifiers must be nonempty, at most 1024 characters and contain no control characters.");
            }
        }

        private static string Segment(string name)
        {
            return Uri.EscapeDataString(name);
        }

        private static InvalidDataException AmbiguousId(string kind, string id)
        {
            return new InvalidDataException($"Field scene {kind} identity '{id}' is ambiguous. Give each item a unique name or a {IdentityProperty} custom ID and re-export.");
        }

        private static float Float(JsonElement owner, string property, float fallback)
        {
            return OptionalFloat(owner, property) ?? fallback;
        }

        private static float? OptionalFloat(JsonElement owner, string property)
        {
            return owner.TryGetProperty(property, out JsonElement value) ? value.GetSingle() : null;
        }

        private static float Range01(float value, string description)
        {
            if (!float.IsFinite(value) || value < 0 || value > 1)
            {
                throw new InvalidDataException($"{description} must be between zero and one.");
            }
            return value;
        }
    }
}
