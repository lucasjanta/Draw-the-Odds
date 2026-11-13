using System.Collections.Generic;
using UnityEngine;

// Monta as plataformas de cada fase clonando um "template" (a plataforma one-way da cena).
// Visual: feltro de mesa de poker (sprite 9-slice cinza, tingido com a cor do chefe, modo Tiled
// para o pontilhado repetir sem esticar). Também espalha props (peões, torres, bolinhas) por cima.
// C# puro: o GameManager cria e chama Build().
public class LevelBuilder
{
    public const float FloorTop = -7.5f;     // o chão ocupa a faixa de baixo da tela (onde fica a mão)
    private const float PlatformHeight = 0.5f;   // colisor (one-way)
    private const float PlatformSurfaceArc = 100f; // ângulo (graus) do topo que colide; o resto é atravessável
    private const float FeltThickness = 1.5f;    // visual: 24 px (bordas do 9-slice têm 8 px em cima e embaixo)

    // (x do centro, y do centro, largura) de cada plataforma, por layout.
    private static readonly Vector3[][] Layouts =
    {
        new[] { new Vector3(-11f, -3f, 10f), new Vector3(11f, -3f, 10f), new Vector3(0f, 1.25f, 12f),
                new Vector3(-12f, 5.5f, 8f), new Vector3(12f, 5.5f, 8f) },
        new[] { new Vector3(-14f, -3f, 8f), new Vector3(0f, -3f, 8f), new Vector3(14f, -3f, 8f),
                new Vector3(-7f, 1.25f, 8f), new Vector3(7f, 1.25f, 8f), new Vector3(0f, 5.5f, 10f) },
        new[] { new Vector3(0f, -3f, 14f), new Vector3(-13f, 1.25f, 10f), new Vector3(13f, 1.25f, 10f),
                new Vector3(-15f, 5.5f, 6f), new Vector3(0f, 5.5f, 6f), new Vector3(15f, 5.5f, 6f) },
    };

    // Superfícies onde dá para spawnar pickups: x mínimo, x máximo, y do topo.
    public struct Surface { public float minX, maxX, top; }

    private readonly Transform template;
    private readonly SpriteRenderer floor;
    private readonly GameConfig config;
    private SpriteRenderer floorFelt;
    private SpriteRenderer background;
    private readonly SpriteRenderer[] wallFelts = new SpriteRenderer[2];
    private readonly Transform[] walls = new Transform[2];
    private readonly Transform root;
    private readonly List<GameObject> spawned = new List<GameObject>();
    private readonly List<Surface> surfaces = new List<Surface>();

    public IReadOnlyList<Surface> Surfaces => surfaces;

    public LevelBuilder(Transform template, SpriteRenderer floor, GameConfig config)
    {
        this.template = template;
        this.floor = floor;
        this.config = config;
        root = new GameObject("Generated Platforms").transform;
        if (template != null) template.gameObject.SetActive(false);

        // Paredes da cena (colisores). Entram um pouco na tela para o feltro delas aparecer.
        var left = GameObject.Find("WallLeft");
        var right = GameObject.Find("WallRight");
        walls[0] = left != null ? left.transform : null;
        walls[1] = right != null ? right.transform : null;
    }

    // Parede: o colisor (1 unidade de largura) fica com a borda de dentro em x = ±WallInner = na borda
    // da tela (−20 a 20). O feltro fica TODO fora da tela: só aparece em telas mais largas que 16:9.
    private const float WallInner = 20f;
    private const float WallFeltWidth = 8f;

    public void Build(int layoutIndex, Color platformColor)
    {
        foreach (var go in spawned) Object.Destroy(go);
        spawned.Clear();
        surfaces.Clear();

        // Tom do feltro: um pouco mais claro que a cor do chefe (o feltro escurece com a borda e o pontilhado).
        // Chão, paredes e plataformas usam o MESMO tom.
        Color feltColor = Color.Lerp(platformColor, Color.white, 0.25f);
        if (floor != null) floor.color = feltColor;
        if (config.Felt != null && floor != null)
        {
            // Chão = um feltro grande do tamanho do chão (o quadrado original só fica com o colisor)
            floor.enabled = false;
            if (floorFelt == null)
            {
                floorFelt = Fx.MakeSprite("FloorFelt", config.Felt, Vector3.zero, Color.white, 1);
                floorFelt.drawMode = SpriteDrawMode.Tiled;
            }
            // O sprite do chão é um quadrado de 1 unidade escalado: posição e escala dão o retângulo.
            Vector3 c = floor.transform.position, size = floor.transform.lossyScale;
            floorFelt.transform.position = new Vector3(c.x, c.y, 0f);
            floorFelt.size = new Vector2(size.x, size.y);
            floorFelt.color = feltColor;
        }
        BuildWalls(feltColor);
        BuildBackground(platformColor);
        surfaces.Add(new Surface { minX = -18f, maxX = 18f, top = FloorTop });

        var layout = Layouts[Mathf.Clamp(layoutIndex, 0, Layouts.Length - 1)];
        foreach (var p in layout)
        {
            var go = Object.Instantiate(template.gameObject, root);
            go.name = "Platform";
            go.SetActive(true);
            go.transform.position = new Vector3(p.x, p.y, 0f);
            go.transform.localScale = new Vector3(p.z, PlatformHeight, 1f);
            // Só o TOPO da plataforma segura o player. Com o arco de 180° as laterais e as quinas
            // também colidiam (o contato de lado tem normal horizontal, bem na beira dos 180°) e
            // travavam o dash/pulo na ponta da plataforma. Com 100° só contatos vindos de cima
            // (até 50° de inclinação) contam; de lado e por baixo o player atravessa.
            if (go.TryGetComponent(out PlatformEffector2D effector))
            {
                effector.surfaceArc = PlatformSurfaceArc;
                effector.useSideFriction = false;
                effector.useSideBounce = false;
            }
            if (go.TryGetComponent(out SpriteRenderer sr))
            {
                sr.color = platformColor;
                if (config.Felt != null)
                {
                    sr.enabled = false; // o quadrado esticado some; fica só o colisor
                    // Feltro como objeto separado (sem a escala do colisor), topo alinhado com o topo da plataforma
                    float top = p.y + PlatformHeight / 2f;
                    var felt = Fx.MakeSprite("Felt", config.Felt, new Vector3(p.x, top - FeltThickness / 2f, 0f), feltColor, 0);
                    felt.drawMode = SpriteDrawMode.Tiled;
                    felt.size = new Vector2(p.z, FeltThickness);
                    spawned.Add(felt.gameObject);
                }
            }
            spawned.Add(go);
            surfaces.Add(new Surface { minX = p.x - p.z / 2f + 1f, maxX = p.x + p.z / 2f - 1f, top = p.y + PlatformHeight / 2f });
        }
        PlaceProps(layoutIndex);
    }

    // Paredes laterais com o mesmo feltro do chão/plataformas. Vão do chão até acima da tela
    // (a ponta arredondada de cima fica escondida) e ficam atrás do chão.
    private void BuildWalls(Color feltColor)
    {
        if (config.Felt == null) return;
        const float bottom = FloorTop - 1f, top = 13f;
        for (int i = 0; i < 2; i++)
        {
            float side = i == 0 ? -1f : 1f;
            if (walls[i] != null)
            {
                walls[i].position = new Vector3(side * (WallInner + 0.5f), walls[i].position.y, 0f);
                if (walls[i].TryGetComponent(out SpriteRenderer wsr)) wsr.enabled = false;
            }
            if (wallFelts[i] == null)
            {
                wallFelts[i] = Fx.MakeSprite(i == 0 ? "WallFeltLeft" : "WallFeltRight", config.Felt, Vector3.zero, Color.white, 0);
                wallFelts[i].drawMode = SpriteDrawMode.Tiled;
            }
            wallFelts[i].transform.position = new Vector3(side * (WallInner + WallFeltWidth / 2f), (bottom + top) / 2f, 0f);
            wallFelts[i].size = new Vector2(WallFeltWidth, top - bottom);
            wallFelts[i].color = feltColor;
        }
    }

    // Fundo: imagem cinza do GameConfig cobrindo a tela, tingida com a cor das plataformas do chefe.
    // Multiplicar cinza × cor = a imagem "pega" a cor sem perder os detalhes.
    private void BuildBackground(Color platformColor)
    {
        Sprite sprite = config.Background;
        if (sprite == null)
        {
            if (background != null) background.enabled = false;
            return;
        }
        if (background == null)
        {
            background = Fx.MakeSprite("Background", sprite, Vector3.zero, Color.white, -100);
            background.drawMode = SpriteDrawMode.Simple;
        }
        background.enabled = true;
        background.sprite = sprite;
        // Escala para cobrir exatamente 40 × 22,5 unidades, seja qual for o PPU da imagem.
        Vector2 size = sprite.bounds.size;
        background.transform.localScale = new Vector3(40f / size.x, 22.5f / size.y, 1f);
        Color tint = Color.Lerp(Color.white, platformColor, config.BackgroundTint) * config.BackgroundBrightness;
        tint.a = 1f;
        background.color = tint;
    }

    // Decoração: 1 prop em cada plataforma e 2 nos cantos do chão. Semente fixa por layout,
    // então a mesma fase sempre tem as mesmas peças no mesmo lugar.
    private void PlaceProps(int layoutIndex)
    {
        if (config.Props == null || config.Props.Count == 0) return;
        var rng = new System.Random(1234 + layoutIndex * 97);
        void Put(float x, float top)
        {
            Sprite s = config.Props[rng.Next(config.Props.Count)];
            if (s == null) return;
            // pivô dos props é embaixo no centro: a peça "fica em pé" no topo da superfície
            var sr = Fx.MakeSprite("Prop", s, new Vector3(Mathf.Round(x * 16f) / 16f, top, 0f), Color.white, 5);
            sr.flipX = rng.Next(2) == 0;
            spawned.Add(sr.gameObject);
        }

        for (int i = 1; i < surfaces.Count; i++) // 0 = chão
        {
            var s = surfaces[i];
            float t = (float)rng.NextDouble();
            Put(Mathf.Lerp(s.minX, s.maxX, t), s.top);
        }
        Put(-18.5f, FloorTop);
        Put(18.5f, FloorTop);
    }

    // Ponto aleatório em cima de alguma superfície, longe do ponto "avoid".
    // others: pontos que também devem ficar longe (outros orbes/pacotes), pelo menos othersDistance.
    public Vector2 RandomSpawnPoint(Vector2 avoid, float minDistance, IList<Vector2> others = null, float othersDistance = 3f)
    {
        Vector2 best = Vector2.zero;
        float bestScore = float.NegativeInfinity;
        for (int i = 0; i < 20; i++)
        {
            var s = surfaces[Random.Range(0, surfaces.Count)];
            var p = new Vector2(Random.Range(s.minX, s.maxX), s.top + 0.9f);
            float d = Vector2.Distance(p, avoid);
            float nearest = float.PositiveInfinity;
            if (others != null) foreach (var o in others) nearest = Mathf.Min(nearest, Vector2.Distance(p, o));
            if (d >= minDistance && nearest >= othersDistance) return p;
            // Nenhum ponto perfeito: fica com o "menos pior"
            float score = Mathf.Min(d - minDistance, nearest - othersDistance);
            if (score > bestScore) { bestScore = score; best = p; }
        }
        return best;
    }
}
