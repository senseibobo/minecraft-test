using Godot;
using System;
using System.Collections.Generic;
using System.Threading;

enum BlockFace {
    TOP,
    BOTTOM,
    LEFT,
    RIGHT,
    FRONT,
    BACK
}

class Chunk {
    public int x;
    public int z;
    public Block[,,] blocks = new Block[16,256,16];

}

class Block {
    public Block(int id, bool transparent) {
        this.id = id;
        this.transparent = transparent;
    }
    int id;
    bool transparent;
    public bool Transparent {get { return transparent; } }
    public int Id { get { return id; } }
}

struct FaceDrawing {
    public int x;
    public int y;
    public int z;
    public BlockFace face;
    public FaceDrawing(int x, int y, int z, int id, BlockFace face) {
        this.x = x;
        this.y = y;
        this.z = z;
        this.face = face;
    }
}

public class Game : Spatial
{

    Dictionary<Tuple<int,int>,Chunk> chunks = new Dictionary<Tuple<int, int>, Chunk>();
    [Export] Texture grassTexture;
    [Export] Godot.Collections.Array<Material> blockMaterials;
    OpenSimplexNoise noise;
    Dictionary<Tuple<int,int>,MeshInstance> chunkMeshes = new Dictionary<Tuple<int, int>, MeshInstance>();
    Dictionary<Tuple<int,int>,CollisionShape> chunkColliders = new Dictionary<Tuple<int, int>, CollisionShape>();

    public override void _Process(float delta) {
        if(Input.IsActionJustPressed("ui_accept")) {
            CreateMap();
        }
    }
    public override void _Ready() {
        CreateMap();
    }
    int ChunkCount = 0;
    private void CreateMap() {
        foreach(MeshInstance meshInstance in chunkMeshes.Values) meshInstance.QueueFree();
        foreach(CollisionShape collider in chunkColliders.Values) collider.QueueFree();
        chunkMeshes.Clear();
        chunkColliders.Clear();
        chunks.Clear();
        ChunkCount = 64;
        noise = new OpenSimplexNoise();
        noise.Seed = (int)GD.Randi();
        for(int i = 0; i < 8; i++) {
            for(int j = 0; j < 8; j++) {
                Chunk chunk = CreateChunk(i*16, j*16);
                chunks.Add(new Tuple<int,int>(i*16,j*16),chunk);
            }
        }
        foreach(Chunk chunk in chunks.Values) {
            DrawChunk(chunk);
        }
    }
    private Chunk CreateChunk(int chunkX, int chunkZ) {
        Chunk chunk = new Chunk();
        chunk.x = chunkX;
        chunk.z = chunkZ;
        Block[,,] blocks = chunk.blocks;
        Tuple<int,int> k = new Tuple<int,int>(chunkX,chunkZ);
        for (int x = 0; x < 16; x++ ) {
            for (int y = 0; y < 256; y++) {
                for (int z = 0; z < 16; z++) {
                    double a = noise.GetNoise2d(chunkX+x,chunkZ + z)*48.0+48.0;
                    if (y < a) {
                        if(x > 8) {
                            blocks[x, y, z] = new Block(1, false);
                        } else {
                            blocks[x, y, z] = new Block(2, false);
                        }
                    } else {
                        blocks[x, y, z] = new Block(0, true);
                    }

                }
            }
        }
        chunk.blocks = blocks;
        return chunk;
    }

    private void DrawChunk(Chunk chunk) {
        SurfaceTool s = new SurfaceTool();
        s.Begin(Mesh.PrimitiveType.Triangles);
        Block[,,] blocks = chunk.blocks;
        int chunkX = chunk.x;
        int chunkZ = chunk.z;
        Dictionary<int,List<FaceDrawing>> facesToDraw = new Dictionary<int,List<FaceDrawing>>();
        ArrayMesh mesh = new ArrayMesh();
        MeshInstance meshInstance = new MeshInstance();
        for(int x = 0; x < 16 ; x++) {
            for(int y = 0 ; y < 256 ; y++) {
                for(int z = 0; z < 16 ; z++) {
                    if(blocks[x,y,z].Id == 0) continue;
                    if(!facesToDraw.ContainsKey(blocks[x,y,z].Id)) {
                        facesToDraw.Add(blocks[x,y,z].Id, new List<FaceDrawing>());
                    }
                    List<FaceDrawing> ftd = facesToDraw[blocks[x,y,z].Id];
                    int fx = chunkX + x;
                    int fz = chunkZ + z;
                    int id = blocks[x,y,z].Id;
                    if(x > 0 && blocks[x-1,y,z].Transparent || x == 0 && CheckChunk(15,y,z,chunkX-16,chunkZ)) ftd.Add(new FaceDrawing(fx,y, fz,id,BlockFace.LEFT));
                    if(y > 0 && blocks[x,y-1,z].Transparent || y == 0) ftd.Add(new FaceDrawing(fx,y, fz,id,BlockFace.BOTTOM));
                    if(z > 0 && blocks[x,y,z-1].Transparent || z == 0 && CheckChunk(x,y,15,chunkX, chunkZ-16)) ftd.Add(new FaceDrawing(fx,y, fz,id,BlockFace.FRONT));                   
                    if(z < 15 && blocks[x,y,z+1].Transparent || z == 15 && CheckChunk(x,y,0,chunkX, chunkZ+16)) ftd.Add(new FaceDrawing(fx,y, fz,id,BlockFace.BACK));
                    if(y < 255 && blocks[x,y+1,z].Transparent || y == 255) ftd.Add(new FaceDrawing(fx,y, fz,id,BlockFace.TOP));
                    if(x < 15 && blocks[x+1,y,z].Transparent || x == 15 && CheckChunk(0,y,z,chunkX+16, chunkZ)) ftd.Add(new FaceDrawing(fx,y,fz,id,BlockFace.RIGHT));;
                }
            }
        }
        foreach(KeyValuePair<int,List<FaceDrawing>> kvp in facesToDraw) {
            s.SetMaterial(blockMaterials[kvp.Key-1]);
            foreach(FaceDrawing f in kvp.Value) {
                AddFace(s, new Vector3(f.x,f.y,f.z),f.face,kvp.Key);
            }
            s.Commit(mesh);
        }
        meshInstance.Mesh = mesh;
        Shape shape = mesh.CreateTrimeshShape();
        CollisionShape collisionShape = new CollisionShape();
        collisionShape.Shape = shape;
        chunkMeshes.Add(new Tuple<int,int>(chunkX,chunkZ),meshInstance);
        chunkColliders.Add(new Tuple<int,int>(chunkX,chunkZ),collisionShape);
        GetNode("Terrain").AddChild(collisionShape);
        AddChild(meshInstance);
    }

    private bool CheckChunk(int x, int y, int z, int chunkX, int chunkZ) {
        if(chunks.ContainsKey(new Tuple<int,int>(chunkX,chunkZ))) {
            return chunks[new Tuple<int,int>(chunkX,chunkZ)].blocks[x,y,z].Transparent;
        } else {
            return false;
        }
    }

    private void AddVertex(SurfaceTool s, Vector3 pos, Vector3 normal, Vector2 uv) {
        s.AddNormal(normal);
        s.AddUv(uv);
        s.AddVertex(pos);
    }


    private void AddFace(SurfaceTool s, Vector3 blockCoordinates, BlockFace face, int blockId) {
        switch(face) {
            case BlockFace.TOP:
                AddVertex(s,new Vector3(blockCoordinates.x, blockCoordinates.y + 1, blockCoordinates.z), new Vector3(0,1,0), new Vector2(0,0));
                AddVertex(s,new Vector3(blockCoordinates.x + 1, blockCoordinates.y + 1, blockCoordinates.z), new Vector3(0,1,0), new Vector2(1,0));
                AddVertex(s,new Vector3(blockCoordinates.x + 1, blockCoordinates.y + 1, blockCoordinates.z + 1), new Vector3(0,1,0), new Vector2(1,1));
                AddVertex(s,new Vector3(blockCoordinates.x, blockCoordinates.y + 1, blockCoordinates.z), new Vector3(0,1,0), new Vector2(0,0));
                AddVertex(s,new Vector3(blockCoordinates.x + 1, blockCoordinates.y + 1, blockCoordinates.z + 1), new Vector3(0,1,0), new Vector2(1,1));
                AddVertex(s,new Vector3(blockCoordinates.x, blockCoordinates.y + 1, blockCoordinates.z + 1), new Vector3(0,1,0), new Vector2(0,1));
                break;
            case BlockFace.BOTTOM:
                AddVertex(s,new Vector3(blockCoordinates.x, blockCoordinates.y, blockCoordinates.z), new Vector3(0,-1,0), new Vector2(0,0));
                AddVertex(s,new Vector3(blockCoordinates.x + 1, blockCoordinates.y, blockCoordinates.z + 1), new Vector3(0,-1,0), new Vector2(1,1));
                AddVertex(s,new Vector3(blockCoordinates.x + 1, blockCoordinates.y, blockCoordinates.z), new Vector3(0,-1,0), new Vector2(1,0));
                AddVertex(s,new Vector3(blockCoordinates.x, blockCoordinates.y, blockCoordinates.z), new Vector3(0,-1,0), new Vector2(0,0));
                AddVertex(s,new Vector3(blockCoordinates.x, blockCoordinates.y, blockCoordinates.z + 1), new Vector3(0,-1,0), new Vector2(0,1));
                AddVertex(s,new Vector3(blockCoordinates.x + 1, blockCoordinates.y, blockCoordinates.z + 1), new Vector3(0,-1,0), new Vector2(1,1));
                break;
            case BlockFace.LEFT:
                AddVertex(s,new Vector3(blockCoordinates.x, blockCoordinates.y, blockCoordinates.z), new Vector3(-1,0,0), new Vector2(0,0));
                AddVertex(s,new Vector3(blockCoordinates.x, blockCoordinates.y + 1, blockCoordinates.z), new Vector3(-1,0,0), new Vector2(1,0));
                AddVertex(s,new Vector3(blockCoordinates.x, blockCoordinates.y + 1, blockCoordinates.z + 1), new Vector3(-1,0,0), new Vector2(1,1));
                AddVertex(s,new Vector3(blockCoordinates.x, blockCoordinates.y, blockCoordinates.z), new Vector3(-1,0,0), new Vector2(0,0));
                AddVertex(s,new Vector3(blockCoordinates.x, blockCoordinates.y + 1, blockCoordinates.z + 1), new Vector3(-1,0,0), new Vector2(1,1));
                AddVertex(s,new Vector3(blockCoordinates.x, blockCoordinates.y, blockCoordinates.z + 1), new Vector3(-1,0,0), new Vector2(0,1));
                break;
            case BlockFace.RIGHT:
                AddVertex(s,new Vector3(blockCoordinates.x + 1, blockCoordinates.y, blockCoordinates.z), new Vector3(1,0,0), new Vector2(0,0));
                AddVertex(s,new Vector3(blockCoordinates.x + 1, blockCoordinates.y + 1, blockCoordinates.z + 1), new Vector3(1,0,0), new Vector2(1,1));
                AddVertex(s,new Vector3(blockCoordinates.x + 1, blockCoordinates.y + 1, blockCoordinates.z), new Vector3(1,0,0), new Vector2(1,0));
                AddVertex(s,new Vector3(blockCoordinates.x + 1, blockCoordinates.y, blockCoordinates.z), new Vector3(1,0,0), new Vector2(0,0));
                AddVertex(s,new Vector3(blockCoordinates.x + 1, blockCoordinates.y, blockCoordinates.z + 1), new Vector3(1,0,0), new Vector2(0,1));
                AddVertex(s,new Vector3(blockCoordinates.x + 1, blockCoordinates.y + 1, blockCoordinates.z + 1), new Vector3(1,0,0), new Vector2(1,1));
                break;
            case BlockFace.FRONT:
                AddVertex(s,new Vector3(blockCoordinates.x, blockCoordinates.y, blockCoordinates.z), new Vector3(0,0,1), new Vector2(0,0));
                AddVertex(s,new Vector3(blockCoordinates.x + 1, blockCoordinates.y, blockCoordinates.z), new Vector3(0,0,1), new Vector2(1,0));
                AddVertex(s,new Vector3(blockCoordinates.x + 1, blockCoordinates.y + 1, blockCoordinates.z), new Vector3(0,0,1), new Vector2(1,1));
                AddVertex(s,new Vector3(blockCoordinates.x, blockCoordinates.y, blockCoordinates.z), new Vector3(0,0,1), new Vector2(0,0));
                AddVertex(s,new Vector3(blockCoordinates.x + 1, blockCoordinates.y + 1, blockCoordinates.z), new Vector3(0,0,1), new Vector2(1,1));
                AddVertex(s,new Vector3(blockCoordinates.x, blockCoordinates.y + 1, blockCoordinates.z), new Vector3(0,0,1), new Vector2(0,1));
                break;
            case BlockFace.BACK:
                AddVertex(s,new Vector3(blockCoordinates.x, blockCoordinates.y, blockCoordinates.z + 1), new Vector3(0,0,-1), new Vector2(0,0));
                AddVertex(s,new Vector3(blockCoordinates.x + 1, blockCoordinates.y + 1, blockCoordinates.z + 1), new Vector3(0,0,-1), new Vector2(1,1));
                AddVertex(s,new Vector3(blockCoordinates.x + 1, blockCoordinates.y, blockCoordinates.z + 1), new Vector3(0,0,-1), new Vector2(1,0));
                AddVertex(s,new Vector3(blockCoordinates.x, blockCoordinates.y, blockCoordinates.z + 1), new Vector3(0,0,-1), new Vector2(0,0));
                AddVertex(s,new Vector3(blockCoordinates.x, blockCoordinates.y + 1, blockCoordinates.z + 1), new Vector3(0,0,-1), new Vector2(0,1));
                AddVertex(s,new Vector3(blockCoordinates.x + 1, blockCoordinates.y + 1, blockCoordinates.z + 1), new Vector3(0,0,-1), new Vector2(1,1));
                break;
        }
    }

}
