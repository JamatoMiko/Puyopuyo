namespace Puyopuyo;

public class Puyo
{
    public static Dictionary<int, Color> PuyoColor = new ()//ぷよのタイプごとの色
    {
        {1, Color.Red},
        {2, Color.Blue},
        {3, Color.Yellow},
        {4, Color.Green},
        {5, Color.Purple}
    };
    //存在しているぷよのリスト（組ぷよ以外）
    public static List <Puyo> puyos = new();
    //ぷよのタイプ
    public int Type { get; set; }
    //現在の座標
    public int PositionX { get; set; }
    public int PositionY { get; set; }
    //現在のマス
    private int _cellX;
    public int CellX
    {
        get => _cellX;
        set
        {
            _cellX = value;
            //PositionX = value * Form1.CellWidth;
        }
    }
    private int _cellY;
    public int CellY
    {
        get => _cellY;
        set
        {
            _cellY = value;
            //PositionY = value * Form1.CellHeight;
        }
    }
    //目標のマス
    public int TargetX;
    public int TargetY;
    public const int BaseSpeed = 8;//基本落下速度
    public int Speed;//落下速度
    //マスの境目を落下中か（アニメーション）
    public bool IsFalling;
    //接地したか
    public bool IsGrounded;
    public Puyo(int type, int cellX, int cellY)
    {
        Type = type;
        CellX = cellX;
        CellY = cellY;
        PositionX = CellX * Form1.CellWidth;
        PositionY = CellY * Form1.CellHeight;
        Speed = BaseSpeed;
    }
    public void Fall()//落下処理
    {
        //衝突するまで下を探索して目標のマスを設定
        var targetX = CellX;
        var targetY = CellY;
        var isBlocked = false;
        while (!isBlocked)
        {
            targetY++;
            isBlocked = Collider(targetX, targetY);
        }
        TargetX = targetX;
        TargetY = targetY - 1;

        if (PositionY < CellY * Form1.CellHeight)//座標が現在のマスに達していない場合
        {
            PositionY += Speed;
            if (PositionY > CellY * Form1.CellHeight)//座標調整
                PositionY = CellY * Form1.CellHeight;
            IsFalling = true;
        }
        else
        {
            IsFalling = false;
        }

        if (!IsFalling)//「落下中」でない場合
        {
            if (CellY < TargetY)//目標のマスに達していない場合
            {
                CellY++;
                IsGrounded = false;
            }
            else//目標のマスに達している場合
            {
                IsGrounded = true;
            }
        }
    }
    public static bool Collider(int targetX, int targetY)//衝突判定
    {
        if (targetX < 0 || targetX >= Form1.StageWidth)//壁
        {
            return true;
        }
        if (targetY < 0 || targetY >= Form1.StageHeight)//天上、床
        {
            return true;
        }
        foreach (var puyo in puyos)//ぷよ
        {
            if (puyo.CellX == targetX && puyo.CellY == targetY)
            {
                return true;
            }
        }
        return false;
    }
    public void Remove()//リストから取り除く
    {
        puyos.Remove(this);
    }
}