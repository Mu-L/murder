﻿using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Murder.Assets;
using Murder.Core.Graphics;
using Murder.Diagnostics;
using Murder.Utilities;
using Murder.Serialization;
using Murder.Core;
using Murder.Core.Geometry;

using Texture2D = Microsoft.Xna.Framework.Graphics.Texture2D;
using Effect = Microsoft.Xna.Framework.Graphics.Effect;
using SpriteFont = Microsoft.Xna.Framework.Graphics.SpriteFont;
using XnaVector3 = Microsoft.Xna.Framework.Vector3;
using Murder.Services;
using Murder.Core.Sounds;

namespace Murder.Data
{
    public partial class GameDataManager : IDisposable
    {
        protected enum ShaderStyle
        {
            Dither,
            Posterize,
        }

        /// <summary>
        /// Maps:
        /// [Game asset type] -> [Guid] 
        /// </summary>
        protected readonly Dictionary<Type, HashSet<Guid>> _database = new();

        /// <summary>
        /// Maps:
        /// [Guid] -> [Asset]
        /// </summary>
        protected readonly Dictionary<Guid, GameAsset> _allAssets = new();

        public readonly CacheDictionary<string, Texture2D> CachedUniqueTextures = new(32);
        public ImmutableArray<string> AvailableUniqueTextures;

        /// <summary>
        /// A larger, 12 pixel tall font TODO: Is this font broken??
        /// </summary>
        public PixelFont LargeFont = null!;

        /// <summary>
        /// A small, 7 pixel tall font
        /// </summary>
        public PixelFont PixelFont = null!;

        /// <summary>
        /// The cheapest and simplest shader. It also has a desaturation/saturation mode.
        /// </summary>
        public Effect ShaderSimple = null!;

        /// <summary>
        /// Actually a fancy shader, has some sprite effect tools for us, like different color blending modes.
        /// </summary>
        public Effect ShaderSprite = null!;

        /// <summary>
        /// A shader for applying colorgrades, "brick" mosaics and enforcing a palette.
        /// </summary>
        public Effect ShaderBricks = null!;

        /// <summary>
        /// A shader for applying colorgrades.
        /// </summary>
        public Effect ShaderColorgrade = null!;

        /// <summary>
        /// Custom optional game shader, provided by <see cref="_game"/>.
        /// </summary>
        public Effect? CustomGameShader = null!;

        public virtual Effect[] OtherEffects { get; } = Array.Empty<Effect>();

        public readonly Dictionary<AtlasId, TextureAtlas> LoadedAtlasses = new();

        public Texture2D TestTexture = null!;
        public Texture2D DitherTexture = null!;

        protected GameProfile? _gameProfile;

        protected string? _assetsBinDirectoryPath;

        public string AssetsBinDirectoryPath => _assetsBinDirectoryPath!;

        private string? _packedBinDirectoryPath;

        public string PackedBinDirectoryPath => _packedBinDirectoryPath!;

        public string BinResourcesDirectoryPath => _binResourcesDirectory!;

        public GameProfile GameProfile
        {
            get
            {
                GameLogger.Verify(_gameProfile is not null, "Why are we acquiring game settings without calling Init() first?");
                return _gameProfile;
            }
            protected set => _gameProfile = value;
        }

        protected virtual GameProfile CreateGameProfile() => _game?.CreateGameProfile() ?? new();

        public const string GameProfileFileName = @"game_config.json";
        
        protected readonly string ShaderRelativePath = Path.Join("shaders", "{0}.mgfxo");

        protected string? _binResourcesDirectory = "resources";

        private readonly IMurderGame? _game;

        /// <summary>
        /// Creates a new game data manager.
        /// </summary>
        /// <param name="game">This is set when overriding Murder utilities.</param>
        public GameDataManager(IMurderGame? game)
        {
            _game = game;
        }

        [MemberNotNull(
            nameof(_binResourcesDirectory),
            nameof(_assetsBinDirectoryPath),
            nameof(_packedBinDirectoryPath))]
        public virtual void Init(string resourcesBinPath = "resources")
        {
            _database.Clear();
            _allAssets.Clear();

            _binResourcesDirectory = resourcesBinPath;

            LoadGameSettings();

            _assetsBinDirectoryPath = FileHelper.GetPath(_binResourcesDirectory, GameProfile.AssetResourcesPath);
            _packedBinDirectoryPath = FileHelper.GetPath(_binResourcesDirectory);
        }

        public virtual void LoadContent()
        {
            RefreshAtlas();
            InitShaders();

            LoadSounds();

            // Clear asset dictionaries for the new assets
            _database.Clear();

            // These will use the atlas as part of the deserialization.
            LoadAssetsAtPath(Path.Join(_binResourcesDirectory, GameProfile.AssetResourcesPath, GameProfile.GenericAssetsPath));
            LoadAssetsAtPath(Path.Join(_binResourcesDirectory, GameProfile.AssetResourcesPath, GameProfile.ContentECSPath));
            LoadAssetsAtPath(Path.Join(_binResourcesDirectory, GameProfile.AssetResourcesPath, GameProfile.ContentAsepritePath));

            LoadAllSaves();
        }

        public virtual void RefreshAtlas()
        {
            GameLogger.Verify(_packedBinDirectoryPath is not null, "Why hasn't LoadContent() been called?");

            DisposeAtlases();
            //FetchAtlas(AtlasId.Gameplay).LoadTextures();

            TestTexture?.Dispose();

            PixelFont = new PixelFont("Pinch");
            LargeFont = new PixelFont("MagicBook");

            // TODO: [Pedro] Load atlas
            var murderFontsFolder = Path.Join(PackedBinDirectoryPath, "fonts");
            var noAtlasFolder = Path.Join(PackedBinDirectoryPath, "images");

            LargeFont.AddFontSize(XmlHelper.LoadXML(Path.Join(PackedBinDirectoryPath, "fonts", "MagicBook.fnt")).DocumentElement!, AtlasId.None);
            PixelFont.AddFontSize(XmlHelper.LoadXML(Path.Join(PackedBinDirectoryPath, "fonts", "Pinch.fnt")).DocumentElement!, AtlasId.None);

            var builder = ImmutableArray.CreateBuilder<string>();
            // TODO: Pedro? Figure out atlas loading.
            // var noAtlasFolder = Path.Join(_contentDirectoryPath, "no_atlas");
            // foreach (var texture in Directory.EnumerateFiles(noAtlasFolder))
            // {
            //    builder.Add(FileHelper.GetPathWithoutExtension(Path.GetRelativePath(noAtlasFolder, texture)));
            // }

            foreach (var texture in Directory.EnumerateFiles(murderFontsFolder))
            {
                if (Path.GetExtension(texture) == ".png")
                {
                    builder.Add(FileHelper.GetPathWithoutExtension(Path.GetRelativePath(PackedBinDirectoryPath, texture)));
                }
            }
            foreach (var texture in Directory.EnumerateFiles(noAtlasFolder))
            {
                if (Path.GetExtension(texture) == ".png")
                {
                    builder.Add(FileHelper.GetPathWithoutExtension(Path.GetRelativePath(PackedBinDirectoryPath, texture)));
                }
            }

            AvailableUniqueTextures = builder.ToImmutable();
        }

        /// <summary>
        /// Override this to load all shaders present in the game.
        /// </summary>
        /// <param name="breakOnFail">Whether we should break if this fails.</param>
        public void LoadShaders(bool breakOnFail)
        {
            GameLogger.Log("Loading Shaders...");
            
            Effect? result = default;
            
            if (LoadShader("bricks", ref result, breakOnFail)) ShaderBricks = result;
            if (LoadShader("colorgrade", ref result, breakOnFail)) ShaderColorgrade = result;
            if (LoadShader("sprite2d", ref result, breakOnFail)) ShaderSprite = result;
            if (LoadShader("simple", ref result, breakOnFail)) ShaderSimple = result;

            if (_game is IShaderProvider provider && provider.Shaders.Length > 0)
            {
                GameLogger.Verify(provider.Shaders.Length == 1, "We didn't implement (yet) loading more than one custom shader.");

                LoadShader(provider.Shaders[0], ref CustomGameShader, breakOnFail);
            }

            GameLogger.Log("...Done!");
        }

        public virtual void InitShaders() { }

        public void InitializeAssets()
        {
            ImmutableDictionary<Guid, GameAsset> dynamicAssets = FilterAllAssetsWithImplementation(typeof(DynamicAsset));

            foreach (var (_, asset) in dynamicAssets)
            {
                ((DynamicAsset)asset).Initialize();
            }
        }

        /// <summary>
        /// Load and return shader of name <paramref name="name"/>.
        /// </summary>
        public bool LoadShader(string name, [NotNullWhen(true)] ref Effect? effect, bool breakOnFail)
        {
            GameLogger.Verify(_packedBinDirectoryPath is not null, "Why hasn't LoadContent() been called?");
            
            if (TryCompileShader(name, out Effect? compiledShader))
            {
                effect = compiledShader;
                return true;
            }

            if (TryLoadShaderFromFile(name, out Effect? shaderFromFile))
            {
                effect = shaderFromFile;
                return true;
            }

            if (breakOnFail)
            {
                throw new InvalidOperationException("Unable to compile shader!");
            }

            return false;
        }
        
        protected virtual bool TryCompileShader(string name, [NotNullWhen(true)] out Effect? result)
        {
            result = null;
            return false;
        }

        private string OutputPathForShaderOfName(string name, string? path = default)
        {
            GameLogger.Verify(_packedBinDirectoryPath is not null, "Why hasn't LoadContent() been called?");
            return Path.Join(path ?? _packedBinDirectoryPath, string.Format(ShaderRelativePath, name));
        }

        private bool TryLoadShaderFromFile(string name, [NotNullWhen(true)] out Effect? result)
        {
            string shaderPath = OutputPathForShaderOfName(name);

            result = null;
            try
            {
                result = new Effect(Game.GraphicsDevice, File.ReadAllBytes(shaderPath));
            }
            catch
            {
                GameLogger.Error($"Error loading file: {shaderPath}");
                return false;
            }

            return true;
        }
        
        private void LoadGameSettings()
        {
            string gameProfilePath = FileHelper.GetPath(Path.Join(_binResourcesDirectory, GameProfileFileName));

            if (_gameProfile is null && FileHelper.Exists(gameProfilePath))
            {
                GameProfile = FileHelper.DeserializeAsset<GameProfile>(gameProfilePath)!;
                GameLogger.Log("Successfully loaded game profile settings.");
            }
#if !DEBUG
            else
            {
                GameLogger.Error("Unable to find the game profile, using a default one. Report this issue immediately!");

                GameProfile = CreateGameProfile();
                GameProfile.MakeGuid();
            }
#endif
        }

        internal MonoWorld CreateWorldInstanceFromSave(Guid guid, Camera2D camera)
        {
            if (TryGetAsset<WorldAsset>(guid) is WorldAsset world)
            {
                // If there is a saved run for this map, run from this!
                if (TryGetActiveSaveData()?.TryLoadLevel(guid) is SavedWorld savedWorld)
                {
                    return world.CreateInstanceFromSave(savedWorld, camera);
                }

                // Otherwise, fallback to default world instances.
                return world.CreateInstance(camera);
            }

            GameLogger.Error($"World asset with guid '{guid}' not found or is corrupted.");
            throw new InvalidOperationException($"World asset with guid '{guid}' not found or is corrupted.");
        }

        private void LoadAssetsAtPath(in string relativePath)
        {
            var fullPath = FileHelper.GetPath(relativePath);
            foreach (var (asset, filepath) in FetchAssetsAtPath(fullPath, skipFailures: true))
            {
                var finalRelative = FileHelper.GetPath(Path.Join(relativePath, FileHelper.Clean(asset.EditorFolder)));
                var filename = Path.GetRelativePath(finalRelative, filepath).ToLowerInvariant().EscapePath();
                var cleanName = asset.Name.ToLowerInvariant().EscapePath() + ".json";

                if (filename != cleanName)
                {
                    GameLogger.Warning($"Inconsistent file and asset name ('{filename}' != '{cleanName}')");
                }

                asset.FilePath = filename;

                AddAsset(asset);
            }
        }

        protected IEnumerable<(GameAsset asset, string filepath)> FetchAssetsAtPath(string fullPath, bool recursive = true, bool skipFailures = true, bool stopOnFailure = false)
        {
            foreach (FileInfo file in FileHelper.GetAllFilesInFolder(fullPath, "*.json", recursive))
            {
                GameAsset? asset = default;
                try
                {
                    asset = FileHelper.DeserializeAsset<GameAsset>(file.FullName);
                }
                catch (Exception ex) when (skipFailures)
                {
                    GameLogger.Warning($"Error loading [{file.FullName}]:{ex}");

                    if (stopOnFailure)
                    {
                        // Immediately stop iterating.
                        yield break;
                    }
                }

                if (asset != null)
                {
                    yield return (asset, file.FullName);
                }
                else
                {
                    GameLogger.Warning($"[{file.FullName}] is not a valid Json file");
                }
            }
        }

        public void RemoveAsset<T>(T asset) where T : GameAsset
        {
            RemoveAsset(asset.GetType(), asset.Guid);
        }

        public void RemoveAsset<T>(Guid assetGuid) where T : GameAsset
        {
            RemoveAsset(typeof(T), assetGuid);
        }

        protected virtual void RemoveAsset(Type t, Guid assetGuid)
        {
            if (!_allAssets.ContainsKey(assetGuid) || !_database.TryGetValue(t, out var databaseSet) || !databaseSet.Contains(assetGuid))
            {
                throw new ArgumentException($"Can't remove asset {assetGuid} from database.");
            }

            _allAssets.Remove(assetGuid);
            databaseSet.Remove(assetGuid);
        }

        public void AddAsset<T>(T asset) where T : GameAsset
        {
            if (!asset.StoreInDatabase)
            {
                // Do not add the asset.
                return;
            }

            if (asset.Guid == Guid.Empty)
            {
                asset.MakeGuid();
            }

            if (string.IsNullOrWhiteSpace(asset.Name))
            {
                asset.Name = asset.Guid.ToString();
            }
            
            // T might correspond to an abstract type.
            // Get the actual implementation type.
            Type t = asset.GetType();
            if (!_database.TryGetValue(t, out HashSet<Guid>? databaseSet))
            {
                databaseSet = new();

                _database[t] = databaseSet;
            }

            if (databaseSet.Contains(asset.Guid) || _allAssets.ContainsKey(asset.Guid))
            {
                GameLogger.Error($"Duplicate assed GUID detected '{_allAssets[asset.Guid].FilePath}, {asset.FilePath}'(GUID:{_allAssets[asset.Guid].Guid})");
                return;
            }

            databaseSet.Add(asset.Guid);
            _allAssets.Add(asset.Guid, asset);
        }

        public bool HasAsset<T>(Guid id) where T : GameAsset =>
            _database.TryGetValue(typeof(T), out HashSet<Guid>? assets) && assets.Contains(id);

        public T? TryGetAsset<T>(Guid id) where T : GameAsset
        {
            if (TryGetAsset(id) is T asset)
            {
                return asset;
            }

            return default;
        }
        public PrefabAsset GetPrefab(Guid id) => GetAsset<PrefabAsset>(id);
        public T GetAsset<T>(Guid id) where T : GameAsset
        {
            if (TryGetAsset<T>(id) is T asset)
            {
                return asset;
            }

            throw new ArgumentException($"Unable to find the asset of type {typeof(T).Name} with id: {id} in database.");
        }

        public GameAsset GetAsset(Guid id)
        {
            if (TryGetAsset(id) is GameAsset asset)
            {
                return asset;
            }

            throw new ArgumentException($"Unable to find the asset with id: {id} in database.");
        }

        /// <summary>
        /// Get a generic asset with a <paramref name="id"/>.
        /// </summary>
        public GameAsset? TryGetAsset(Guid id)
        {
            if (_allAssets.TryGetValue(id, out GameAsset? asset))
            {
                return asset;
            }

            return default;
        }

        public ImmutableArray<GameAsset> GetAllAssets() => 
            _allAssets.Values.ToImmutableArray();

        /// <summary>
        /// Find all the assets names for an asset type <paramref name="t"/>.
        /// </summary>
        /// <param name="t">The type that inherist from <see cref="GameAsset"/>.</param>
        public ImmutableHashSet<string> FindAllNamesForAsset(Type t)
        {
            ImmutableHashSet<string> result = ImmutableHashSet<string>.Empty;

            if (_database.TryGetValue(t, out HashSet<Guid>? assetGuids))
            {
                result = assetGuids.Select(g => _allAssets[g].Name).ToImmutableHashSet(StringComparer.OrdinalIgnoreCase);
            }

            return result;
        }

        public ImmutableDictionary<Guid, GameAsset> FilterAllAssets(params Type[] types)
        {
            var builder = ImmutableDictionary.CreateBuilder<Guid, GameAsset>();

            foreach (var t in types)
            {
                if (_database.TryGetValue(t, out HashSet<Guid>? assetGuids))
                {
                    builder.AddRange(assetGuids.ToDictionary(id => id, id => _allAssets[id]));
                }
            }

            return builder.ToImmutableDictionary();
        }

        /// <summary>
        /// Filter all the assets and any types that implement those types.
        /// Cautious: this may be slow or just imply extra allocations.
        /// </summary>
        public ImmutableDictionary<Guid, GameAsset> FilterAllAssetsWithImplementation(params Type[] types)
        {
            var builder = ImmutableDictionary.CreateBuilder<Guid, GameAsset>();

            builder.AddRange(FilterAllAssets(types));

            foreach (var t in types)
            {
                // If the type is abstract, also gather all the assets that implement it.
                if (t.IsAbstract)
                {
                    foreach (Type assetType in _database.Keys)
                    {
                        if (t.IsAssignableFrom(assetType))
                        {
                            builder.AddRange(FilterAllAssets(assetType));
                        }
                    }
                }
            }

            return builder.ToImmutableDictionary();
        }

        /// <summary>
        /// Return all the assets except the ones in <paramref name="types"/>.
        /// </summary>
        public ImmutableDictionary<Guid, GameAsset> FilterOutAssets(params Type[] types)
        {
            var builder = ImmutableDictionary.CreateBuilder<Guid, GameAsset>();

            foreach (Type type in _database.Keys)
            {
                if (!types.Contains(type))
                {
                    builder.AddRange(FilterAllAssets(type));
                }
            }

            return builder.ToImmutableDictionary();
        }

        public void Dispose()
        {
            DisposeAtlases();
        }

        public Texture2D FetchTexture(string path)
        {
            if (CachedUniqueTextures.ContainsKey(path))
            {
                return CachedUniqueTextures[path];
            }

            var texture = TextureServices.FromFile(Game.GraphicsDevice, Path.Join(_packedBinDirectoryPath, $"{path}.png"), true);
            CachedUniqueTextures[path] = texture;

            return texture;
        }

        public TextureAtlas FetchAtlas(AtlasId atlas)
        {
            if (atlas == AtlasId.None)
            {
                throw new ArgumentException("There's no atlas to fetch.");
            }

            if (!LoadedAtlasses.ContainsKey(atlas))
            {
                TextureAtlas? newAtlas = FileHelper.DeserializeGeneric<TextureAtlas>(
                    Path.Join(_packedBinDirectoryPath, GameProfile.AtlasFolderName, $"{atlas.GetDescription()}.json"));

                if (newAtlas is not null)
                {
                    LoadedAtlasses[atlas] = newAtlas;
                }
                else
                {
                    GameLogger.Warning($"Skipping atlas: {atlas} because it was not found.");

                    // Create dummy data.
                    LoadedAtlasses[atlas] = new("Empty", atlas);
                }
            }

            return LoadedAtlasses[atlas];
        }

        public TextureAtlas? TryFetchAtlas(AtlasId atlas)
        {
            if (atlas == AtlasId.None)
            {
                return null;
            }

            if (!LoadedAtlasses.ContainsKey(atlas))
            {
                string filepath = Path.Join(_packedBinDirectoryPath, GameProfile.AtlasFolderName, $"{atlas.GetDescription()}.json");
                TextureAtlas? newAtlas = FileHelper.DeserializeGeneric<TextureAtlas>(filepath, warnOnErrors: false);

                if (newAtlas is not null)
                {
                    LoadedAtlasses[atlas] = newAtlas;
                }
            }

            if (LoadedAtlasses.TryGetValue(atlas, out TextureAtlas? texture))
            {
                return texture;
            }

            return null;
        }

        public void DisposeAtlases()
        {
            foreach (var atlas in LoadedAtlasses)
            {
                atlas.Value?.Dispose();
            }

            LoadedAtlasses.Clear();
        }
    }
}