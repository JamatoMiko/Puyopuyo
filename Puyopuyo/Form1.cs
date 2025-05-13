using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Linq;

namespace Puyopuyo;

//TODO アニメーションの遅延frame、連結状態がわかるように差分、CPU対戦

public partial class Form1 : Form
{
    int _numberOfColor = 4;//ぷよの色数（1～5まで）
    int[,] _startingBoard = {//初期盤面、ぷよのタイプが入る
        //緑2個でGTR
        /*
        {0, 0, 0, 0, 0, 0},//0
        {0, 0, 0, 0, 3, 0},//1
        {0, 0, 0, 0, 3, 2},//2
        {0, 0, 0, 0, 3, 1},//3
        {0, 4, 3, 4, 4, 3},//4
        {1, 3, 4, 3, 2, 1},//5
        {4, 4, 3, 2, 1, 1},//6
        {3, 3, 4, 3, 2, 2},//7
        {3, 4, 2, 3, 4, 2},//8
        {4, 4, 2, 4, 2, 4},//9
        {1, 3, 2, 1, 2, 4},//10
        {1, 1, 3, 2, 1, 2},//11
        {3, 3, 2, 1, 1, 2}//12
        */
        {0, 0, 0, 0, 0, 0},//0
        {0, 0, 0, 0, 0, 0},//1
        {0, 0, 0, 0, 0, 0},//2
        {0, 0, 0, 0, 0, 0},//3
        {0, 0, 0, 0, 0, 0},//4
        {0, 0, 0, 0, 0, 0},//5
        {0, 0, 0, 0, 0, 0},//6
        {0, 0, 0, 0, 0, 0},//7
        {0, 0, 0, 0, 0, 0},//8
        {0, 0, 0, 0, 0, 0},//9
        {0, 0, 0, 0, 0, 0},//10
        {0, 0, 0, 0, 0, 0},//11
        {0, 0, 0, 0, 0, 0}//12
    };
    Dictionary<int, int> ChainBonus = new ()//連鎖ボーナス
    {
        {1, 0},
        {2, 8},
        {3, 16},
        {4, 32},
        {5, 64},
        {6, 96},
        {7, 128},
        {8, 160},
        {9, 192},
        {10, 224},
        {11, 256},
        {12, 288},
        {13, 320},
        {14, 352},
        {15, 286},
        {16, 416},
        {17, 448},
        {18, 480},
        {19, 512}
    };
    Dictionary<int, int> ConnectBonus = new ()//連結ボーナス
    {
        {4, 1},
        {5, 2},
        {6, 3},
        {7, 4},
        {8, 5},
        {9, 6},
        {10, 7},
        {11, 10}
    };
    Dictionary<int, int> ColorBonus = new()//色数ボーナス
    {
        {1, 0},
        {2, 3},
        {3, 6},
        {4, 12},
        {5, 24}
    };
    int _chain;//連鎖
    int _fallBonus;//落下ボーナス
    public int Score { get; set; }//スコア
    Random random = new Random();
    System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
    public const int StageWidth = 6;//ステージの幅
    public const int StageHeight = 13;//ステージの高さ
    public const int CellWidth = 32;//マスの幅
    public const int CellHeight = 32;//マスの高さ
    Queue<(int Main, int Sub)> _nextPuyos;//ネクストぷよのタイプのキュー
    Puyo? mainPuyo, subPuyo;//組ぷよ
    bool _speedUp;//高速落下中か
    bool _completed;//全消しフラグ
    (int X, int Y)[] _offsets = {//ぷよの相対位置
        (1, 0),//0
        (0, -1),//90
        (-1, 0),//180
        (0, 1)//270
    };
    int _angle;//サブぷよの角度（_offsetsのインデックス番号）
    public enum StageState//ステージの状態
    {
        Controlling,//プレイヤーが操作中
        Connecting,//連結・消去処理
        Falling,//落下処理
        Pausing,//一時停止
        GameOver//ゲームオーバー
    }
    StageState currentState;//現在のステージの状態
    StageState previousState;//ポーズする前のステージの状態
    int _animationDelay;
    public Form1()
    {
        InitializeComponent();

        InitializeBoard();

        timer.Interval = 16;//ミリ秒
        timer.Tick += (sender, e) => {
            UpdateStage();
            this.Invalidate();
        };
        timer.Start();

        this.DoubleBuffered = true;
    }
    void InitializeBoard()
    {
        _animationDelay = 0;

        Score = 0;
        _chain = 0;
        _fallBonus = 0;
        _completed = false;

        Puyo.puyos = new();
        for (int row = 0; row < StageHeight; row++)
        {
            for (int column = 0; column < StageWidth; column++)
            {
                if (_startingBoard[row, column] > 0)
                {
                    Puyo.puyos.Add(new Puyo(_startingBoard[row, column], column, row));
                }
            }
        }

        _nextPuyos = new ();
        //_nextPuyos.Enqueue((4, 4));
        _nextPuyos.Enqueue((random.Next(1, _numberOfColor + 1), random.Next(1, _numberOfColor + 1)));
        _nextPuyos.Enqueue((random.Next(1, _numberOfColor + 1), random.Next(1, _numberOfColor + 1)));

        if (CreatePuyo()) currentState = StageState.Controlling;
    }
    bool CreatePuyo()
    {
        //左から３列目にぷよを作成、埋まっていたらfalseを返す
        foreach (var puyo in Puyo.puyos)
        {
            if (puyo.CellX == 2 && puyo.CellY == 1)
            {
                return false;
            }
        }
        var nextPuyo = _nextPuyos.Dequeue();
        mainPuyo = new Puyo(nextPuyo.Main, 2, 0);
        subPuyo = new Puyo(nextPuyo.Sub, 2, -1);
        _angle = 1;//90
        _nextPuyos.Enqueue((random.Next(1, _numberOfColor + 1), random.Next(1, _numberOfColor + 1)));
        return true;
    }
    void UpdateStage()
    {
        switch (currentState)
        {
            case StageState.Controlling://組ぷよの処理
                //下にあるぷよから処理
                //どちらかが着地するまでは高速落下で1マス落下するごとに落下ボーナスを加点、着地でさらに加点
                //どちらかが着地したらもう一方のぷよはベーススピードで落下
                //着地点を描画
                //どちらかが着地したら両方とも組ぷよから削除、連結処理ではなく落下処理へ移行、その時点で着地点の描画はやめる
                if (mainPuyo != null && subPuyo != null)
                {
                    List<Puyo> fallingPuyos = new() { mainPuyo, subPuyo };
                    //Y座標の降順（下にあるぷよから処理）
                    fallingPuyos = fallingPuyos.OrderByDescending(p => p.PositionY).ToList();
                    var hasFallBonusBeenAdded = false;
                    foreach (var puyo in fallingPuyos)
                    {
                        puyo.Speed = _speedUp ? 8 : 1;//スピードを設定
                        puyo.Fall();
                        if (_speedUp && !puyo.IsFalling && !hasFallBonusBeenAdded)//高速落下で1マス落下するごとに加点
                        {
                            _fallBonus++;
                            hasFallBonusBeenAdded = true;
                        }
                        //どちらかが着地したら両方をpuyosに追加
                        if (!puyo.IsFalling && puyo.IsGrounded)
                        {
                            if (_speedUp) _fallBonus++;//着地ボーナス
                            mainPuyo.Speed = Puyo.BaseSpeed;//スピードをベーススピードに
                            subPuyo.Speed = Puyo.BaseSpeed;
                            Puyo.puyos.Add(mainPuyo);
                            Puyo.puyos.Add(subPuyo);
                            mainPuyo = null;
                            subPuyo = null;
                            Score += _fallBonus;//スコアに加算
                            _fallBonus = 0;
                            currentState = StageState.Falling;
                            break;//どちらかが着地したら処理を終了
                        }
                    }
                }
                break;
            case StageState.Connecting://連結・消去処理
                //探索を開始するぷよを決める
                //探索して連結しているぷよのリストを作る
                //探索が終わったらリストのリストを作る
                //探索木search tree
                //探索の開始点、上下左右のぷよのタイプが同じかつまだリストに存在していない場合、追加してから待機リストから取り除く
                //枝分かれする場合
                //上下左右を探索、同じタイプのぷよがある場合リストに追加（重複しないように）
                //来た方向以外を探索する
                //上下左右を探索、タイプが同じでまだリストに追加されていないぷよがあった場合、枝分かれのリストに追加する
                //上下左右を探索、タイプが同じでまだリストに追加されていないぷよがない場合探索を終了、前の枝に戻る
                var puyoGroups = new List<List<Puyo>>();//連結しているぷよのグループ
                var unfinishedPuyos = Puyo.puyos.ToList();//未了ぷよ
                var waitingPuyos = new Queue<Puyo>();//待機中ぷよ
                while (unfinishedPuyos.Count > 0)
                {
                    var connectingPuyos = new List<Puyo>();//連結ぷよ
                    waitingPuyos.Enqueue(unfinishedPuyos[0]);//未了ぷよの先頭を追加
                    while (waitingPuyos.Count > 0)
                    {
                        var searchingPuyo = waitingPuyos.Dequeue();//待機中ぷよの先頭を取り出し
                        connectingPuyos.Add(searchingPuyo);//連結ぷよのリストに追加
                        unfinishedPuyos.Remove(searchingPuyo);//未了ぷよから取り除く
                        foreach(var puyo in Puyo.puyos)
                        {
                            if (puyo.CellX == searchingPuyo.CellX + 1 && puyo.CellY == searchingPuyo.CellY)//右
                            {
                                if (puyo.Type == searchingPuyo.Type)//同じタイプ
                                {
                                    if (!connectingPuyos.Contains(puyo))//まだ連結ぷよのリストにない
                                    {
                                        if (!waitingPuyos.Contains(puyo))//まだ待機中のリストにない
                                        {
                                        waitingPuyos.Enqueue(puyo);//待機中ぷよのキューに追加
                                        }
                                    }
                                }
                            }
                            if (puyo.CellX == searchingPuyo.CellX && puyo.CellY == searchingPuyo.CellY - 1)//上
                            {
                                if (puyo.Type == searchingPuyo.Type)//同じタイプ
                                {
                                    if (!connectingPuyos.Contains(puyo))//まだ連結ぷよのリストにない
                                    {
                                        if (!waitingPuyos.Contains(puyo))//まだ待機中のリストにない
                                        {
                                            waitingPuyos.Enqueue(puyo);//待機中ぷよのキューに追加
                                        }
                                    }
                                }
                            }
                            if (puyo.CellX == searchingPuyo.CellX - 1 && puyo.CellY == searchingPuyo.CellY)//左
                            {
                                if (puyo.Type == searchingPuyo.Type)//同じタイプ
                                {
                                    if (!connectingPuyos.Contains(puyo))//まだ連結ぷよのリストにない
                                    {
                                        if (!waitingPuyos.Contains(puyo))//まだ待機中のリストにない
                                        {
                                            waitingPuyos.Enqueue(puyo);//待機中ぷよのキューに追加
                                        }
                                    }
                                }
                            }
                            if (puyo.CellX == searchingPuyo.CellX && puyo.CellY == searchingPuyo.CellY + 1)//下
                            {
                                if (puyo.Type == searchingPuyo.Type)//同じタイプ
                                {
                                    if (!connectingPuyos.Contains(puyo))//まだ連結ぷよのリストにない
                                    {
                                        if (!waitingPuyos.Contains(puyo))//まだ待機中のリストにない
                                        {
                                            waitingPuyos.Enqueue(puyo);//待機中ぷよのキューに追加
                                        }
                                    }
                                }
                            }
                        }
                    }
                    puyoGroups.Add(connectingPuyos);//連結ぷよをグループに追加
                }
                //消去処理
                var removed = false;
                var removedPuyos = 0;//消したぷよの数
                var removedColor = new List<int>();//消した色数
                var totalConnectBonus = 0;//連結ボーナスの合計
                foreach (var group in puyoGroups.ToList())//例外が発生しないようにコピーを取る
                {
                    if (group.Count >= 4)
                    {
                        foreach (var puyo in group)
                        {
                            if (!removedColor.Contains(puyo.Type))
                                removedColor.Add(puyo.Type);
                            puyo.Remove();
                            removedPuyos++;
                        }
                        totalConnectBonus += ConnectBonus[group.Count];
                        puyoGroups.Remove(group);
                        removed = true;
                    }
                }
                if (removed)
                {
                    _chain++;//連鎖を加算
                    //Debug.WriteLine($"{_chain}れんさ！");
                    //得点計算　消したぷよの個数×（連鎖ボーナス＋連結ボーナスの合計＋色数ボーナス）×10
                    Score += removedPuyos * (ChainBonus[_chain] + totalConnectBonus + ColorBonus[removedColor.Count]) * 10;
                    if (Puyo.puyos.Count == 0)//全消し
                    {
                        //_chain = 0;
                        _completed = true;
                        Score += 1800;
                    }
                    currentState = StageState.Falling;//落下処理へ移行
                }
                else
                {
                    _chain = 0;//連鎖を初期化
                    _completed = false;
                    if (CreatePuyo())//組ぷよを作成
                    {
                        currentState = StageState.Controlling;//組ぷよの操作へ移行
                    }
                    else
                    {
                        currentState = StageState.GameOver;//ゲームオーバー
                    }
                }
                break;
            case StageState.Falling://落下処理
                //すべてのぷよが着地したら再び連結処理
                //落下処理、下にある（Y座標が大きい）ぷよから順に
                var isAllGrounded = true;
                Puyo.puyos = Puyo.puyos.OrderByDescending(item => item.PositionY).ToList();//Y座標の降順にソート（下にある順）
                foreach (var puyo in Puyo.puyos)
                {
                    puyo.Fall();
                    if (puyo.IsFalling || !puyo.IsGrounded)
                        isAllGrounded = false;
                }
                if (isAllGrounded)
                {
                    _animationDelay++;
                    if (_animationDelay >= 10)
                    {
                        _animationDelay = 0;
                        currentState = StageState.Connecting;//連結処理へ移行
                    }
                }
                break;
            case StageState.GameOver:
                _animationDelay++;
                break;
        }
    }
    void RotatePuyo(int direction = -1)//-1:時計回り 1:反時計回り
    {
        if (mainPuyo != null && subPuyo != null)//メインぷよとサブぷよがどちらも着地していない場合
        {
            var targetAngle = _angle += direction;
            if (targetAngle > 3)
                targetAngle = 0;
            if (targetAngle < 0)
                targetAngle = 3;
            //目標のマスが衝突する場合メインぷよを押し返す、押し返した先が衝突する場合回転させない→さらに回転させる（クイックターン）
            if (Puyo.Collider(mainPuyo.CellX + _offsets[targetAngle].X, mainPuyo.CellY + _offsets[targetAngle].Y))
            {
                if (Puyo.Collider(mainPuyo.CellX - _offsets[targetAngle].X, mainPuyo.CellY - _offsets[targetAngle].Y))
                {
                    _angle = targetAngle;
                    RotatePuyo(direction);//更に回転させる
                    return;
                }
                if (_offsets[targetAngle].X != 0)//変更がある場合のみ
                {
                    mainPuyo.CellX -= _offsets[targetAngle].X;
                    mainPuyo.PositionX = mainPuyo.CellX * CellWidth;
                }
                if (_offsets[targetAngle].Y != 0)//変更がある場合のみ
                {
                    mainPuyo.CellY -= _offsets[targetAngle].Y;
                    mainPuyo.PositionY = mainPuyo.CellY * CellHeight;
                }
            }
            _angle = targetAngle;//角度を変更
            subPuyo.CellX = mainPuyo.CellX + _offsets[_angle].X;
            subPuyo.CellY = mainPuyo.CellY + _offsets[_angle].Y;
            subPuyo.PositionX = mainPuyo.PositionX + _offsets[_angle].X * CellWidth;
            subPuyo.PositionY = mainPuyo.PositionY + _offsets[_angle].Y * CellHeight;
        }
    }
    void MovePuyoRight()//右移動
    {
        if (mainPuyo != null && subPuyo != null)
        {
            //_angleで判断
            //サブぷよが右にいる場合、サブぷよで当たり判定
            //サブぷよが左にいる場合、メインぷよで当たり判定
            //サブぷよが上か下にいる場合、両方で当たり判定、どちらかが衝突する場合移動できない
            if (_angle == 0)//0、右
            {
                if (!Puyo.Collider(subPuyo.CellX + 1, subPuyo.CellY))
                {
                    mainPuyo.CellX++;
                    mainPuyo.PositionX = mainPuyo.CellX * CellWidth;
                    subPuyo.CellX++;
                    subPuyo.PositionX = subPuyo.CellX * CellWidth;
                }
            }
            if (_angle == 2)//180、左
            {
                if (!Puyo.Collider(mainPuyo.CellX + 1, mainPuyo.CellY))
                {
                    mainPuyo.CellX++;
                    mainPuyo.PositionX = mainPuyo.CellX * CellWidth;
                    subPuyo.CellX++;
                    subPuyo.PositionX = subPuyo.CellX * CellWidth;
                }
            }
            if (_angle == 1 || _angle == 3)//90、270、上下
            {
                if (!Puyo.Collider(mainPuyo.CellX + 1, mainPuyo.CellY) && !Puyo.Collider(subPuyo.CellX + 1, subPuyo.CellY))
                {
                    mainPuyo.CellX++;
                    mainPuyo.PositionX = mainPuyo.CellX * CellWidth;
                    subPuyo.CellX++;
                    subPuyo.PositionX = subPuyo.CellX * CellWidth;
                }
            }
        }
    }
    void MovePuyoLeft()//左移動
    {
        if (mainPuyo != null && subPuyo != null)
        {
            //_angleで判断
            //サブぷよが右にいる場合、メインぷよで当たり判定
            //サブぷよが左にいる場合、サブぷよで当たり判定
            //サブぷよが上か下にいる場合、両方で当たり判定、どちらかが衝突する場合移動できない
            if (_angle == 0)//0、右
            {
                if (!Puyo.Collider(mainPuyo.CellX - 1, mainPuyo.CellY))
                {
                    mainPuyo.CellX--;
                    mainPuyo.PositionX = mainPuyo.CellX * CellWidth;
                    subPuyo.CellX--;
                    subPuyo.PositionX = subPuyo.CellX * CellWidth;
                }
            }
            if (_angle == 2)//180、左
            {
                if (!Puyo.Collider(subPuyo.CellX - 1, subPuyo.CellY))
                {
                    mainPuyo.CellX--;
                    mainPuyo.PositionX = mainPuyo.CellX * CellWidth;
                    subPuyo.CellX--;
                    subPuyo.PositionX = subPuyo.CellX * CellWidth;
                }
            }
            if (_angle == 1 || _angle == 3)//90、270、上下
            {
                if (!Puyo.Collider(mainPuyo.CellX - 1, mainPuyo.CellY) && !Puyo.Collider(subPuyo.CellX - 1, subPuyo.CellY))
                {
                    mainPuyo.CellX--;
                    mainPuyo.PositionX = mainPuyo.CellX * CellWidth;
                    subPuyo.CellX--;
                    subPuyo.PositionX = subPuyo.CellX * CellWidth;
                }
            }
        }
    }
    protected override void OnKeyDown(KeyEventArgs e)//キー入力
    {
        base.OnKeyDown(e);
        var key = e.KeyCode;
        //ゲームオーバーになって一定時間経ったときにいずれかのキーを押すとリスタート
        if (currentState == StageState.GameOver && _animationDelay >= 10)
        {
            InitializeBoard();
            return;
        }
        if (key == Keys.Up)//時計回り
        {
            RotatePuyo(-1);
        }
        if (key == Keys.Down)//反時計回り
        {
            RotatePuyo(1);
        }
        if (key == Keys.Left)//移動左
        {
            MovePuyoLeft();
        }
        if (key == Keys.Right)//移動右
        {
            MovePuyoRight();
        }
        if (key == Keys.Space)//高速落下
        {
            _speedUp = true;
        }
        if (key == Keys.Escape)
        {
            if (currentState != StageState.Pausing)
            {
                previousState = currentState;
                currentState = StageState.Pausing;
            }
            else
            {
                currentState = previousState;
            }
        }
        if (key == Keys.R)
        {
            if (currentState == StageState.Pausing)//ポーズ中にRキーでリスタート
            {
                InitializeBoard();
                return;
            }
        }
    }
    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);
        var key = e.KeyCode;
        if (key == Keys.Space)
        {
            _speedUp = false;
        }
    }
    protected override void OnPaint(PaintEventArgs e)//描画処理
    {
        base.OnPaint(e);
        var g = e.Graphics;
        //ステージ
        g.DrawRectangle(new Pen(Color.Black), 0, CellHeight, 6 * CellWidth, 12 * CellHeight);
        /*
        for (int y = 0; y < StageHeight - 1; y++){
            for (int x = 0; x < StageWidth; x++)
            {
                g.DrawRectangle(new Pen(Color.Black), x * CellWidth, (y + 1) * CellHeight, CellWidth, CellHeight);
            }
        }
        */
        //バツ印
        g.FillPolygon(
            new SolidBrush(Color.Red),
            new Point[]{
                new Point(2 * CellWidth, CellHeight + 8),
                new Point(2 * CellWidth + 8, CellHeight),
                new Point(3 * CellWidth, 2 * CellHeight - 8),
                new Point(3 * CellWidth - 8, 2 * CellHeight)
            }
        );
        g.FillPolygon(
            new SolidBrush(Color.Red),
            new Point[]{
                new Point(2 * CellWidth, 2 * CellHeight - 8),
                new Point(3 * CellWidth - 8, CellHeight),
                new Point(3 * CellWidth, CellHeight + 8),
                new Point(2 * CellWidth + 8, 2 * CellHeight)
            }
        );
        //スコア
        g.DrawString($"{Score}", new Font("Arial", 32), new SolidBrush(Color.Black), 0, StageHeight * CellHeight);
        //ネクストぷよ
        g.DrawString("NEXT", new Font("Arial", 16), new SolidBrush(Color.Black), StageWidth * CellWidth + 4, 4);
        var nextPuyos = _nextPuyos.ToArray();//キューからリストに
        DrawPuyo(g, Puyo.PuyoColor[nextPuyos[0].Main], StageWidth * CellWidth + 4, 2 * CellHeight);
        DrawPuyo(g, Puyo.PuyoColor[nextPuyos[0].Sub], StageWidth * CellWidth + 4, CellHeight);
        DrawPuyo(g, Puyo.PuyoColor[nextPuyos[1].Main], (StageWidth + 1) * CellWidth + 8, 3 * CellHeight);
        DrawPuyo(g, Puyo.PuyoColor[nextPuyos[1].Sub], (StageWidth + 1) * CellWidth + 8, 2 * CellHeight);
        //ぷよ
        List<Puyo> drawingPuyos = new();
        if (mainPuyo != null)
            drawingPuyos.Add(mainPuyo);
        if (subPuyo != null)
            drawingPuyos.Add(subPuyo);
        drawingPuyos.AddRange(Puyo.puyos);
        foreach (var puyo in drawingPuyos)
        {
            DrawPuyo(g, Puyo.PuyoColor[puyo.Type], puyo.PositionX, puyo.PositionY);
        }
        //着地点
        if (mainPuyo != null)
        {
            var cy = 0;
            if (_angle == 3)
                cy = 1;
            if (mainPuyo.TargetY > 0)
                g.DrawEllipse(new Pen(Puyo.PuyoColor[mainPuyo.Type]), mainPuyo.TargetX * CellWidth + 1, (mainPuyo.TargetY - cy)* CellHeight + 1, CellWidth - 2, CellHeight - 2);
        }
        if (subPuyo != null)
        {
            var cy = 0;
            if (_angle == 1)
                cy = 1;
            if (subPuyo.TargetY > 0)
                g.DrawEllipse(new Pen(Puyo.PuyoColor[subPuyo.Type]), subPuyo.TargetX * CellWidth + 1, (subPuyo.TargetY - cy) * CellHeight + 1, CellWidth - 2, CellHeight - 2);
        }
        //れんさ
        if (_chain > 0)
        {
            g.DrawString($"{_chain}れんさ！", new Font("Arial", 8 + 4 * _chain), new SolidBrush(Color.DarkOrange), 2 * CellWidth - 1, 5 * CellHeight);
            g.DrawString($"{_chain}れんさ！", new Font("Arial", 8 + 4 * _chain), new SolidBrush(Color.DarkOrange), 2 * CellWidth + 1, 5 * CellHeight);
            g.DrawString($"{_chain}れんさ！", new Font("Arial", 8 + 4 * _chain), new SolidBrush(Color.DarkOrange), 2 * CellWidth, 5 * CellHeight - 1);
            g.DrawString($"{_chain}れんさ！", new Font("Arial", 8 + 4 * _chain), new SolidBrush(Color.DarkOrange), 2 * CellWidth, 5 * CellHeight + 1);
            g.DrawString($"{_chain}れんさ！", new Font("Arial", 8 + 4 * _chain), new SolidBrush(Color.Orange), 2 * CellWidth, 5 * CellHeight);
        }
        //全消し
        if (_completed)
        {
            g.DrawString("全消し！", new Font("Arial", 32), new SolidBrush(Color.DarkOrange), 1 * CellWidth - 1, 5 * CellHeight);
            g.DrawString("全消し！", new Font("Arial", 32), new SolidBrush(Color.DarkOrange), 1 * CellWidth + 1, 5 * CellHeight);
            g.DrawString("全消し！", new Font("Arial", 32), new SolidBrush(Color.DarkOrange), 1 * CellWidth, 5 * CellHeight - 1);
            g.DrawString("全消し！", new Font("Arial", 32), new SolidBrush(Color.DarkOrange), 1 * CellWidth, 5 * CellHeight + 1);
            g.DrawString("全消し！", new Font("Arial", 32), new SolidBrush(Color.White), 1 * CellWidth, 5 * CellHeight);
        }
        //ゲームオーバー
        if (currentState == StageState.GameOver)
        {
            g.DrawString("ばたんきゅ～", new Font("Arial", 32), new SolidBrush(Color.DarkBlue), 1 * CellWidth - 1, 5 * CellHeight);
            g.DrawString("ばたんきゅ～", new Font("Arial", 32), new SolidBrush(Color.DarkBlue), 1 * CellWidth + 1, 5 * CellHeight);
            g.DrawString("ばたんきゅ～", new Font("Arial", 32), new SolidBrush(Color.DarkBlue), 1 * CellWidth, 5 * CellHeight - 1);
            g.DrawString("ばたんきゅ～", new Font("Arial", 32), new SolidBrush(Color.DarkBlue), 1 * CellWidth, 5 * CellHeight + 1);
            g.DrawString("ばたんきゅ～", new Font("Arial", 32), new SolidBrush(Color.White), 1 * CellWidth, 5 * CellHeight);
        }
        //一時停止
        if (currentState == StageState.Pausing)
        {
            g.FillRectangle(new SolidBrush(Color.White), 1 * CellWidth, 5 * CellHeight, 4 * CellWidth, 1 * CellHeight);
            g.DrawRectangle(new Pen(Color.Black), 1 * CellWidth, 5 * CellHeight, 4 * CellWidth, 1 * CellHeight);
            g.DrawString("一時停止中", new Font("Arial", CellWidth / 2), new SolidBrush(Color.Black), 1 * CellWidth + CellWidth / 6, 5 * CellHeight + CellHeight / 6);
            g.DrawString("[Esc]:再開", new Font("Arial", CellWidth / 4), new SolidBrush(Color.Black), 2 * CellWidth, 6 * CellHeight + 4);
            g.DrawString("[R]:リスタート", new Font("Arial", CellWidth / 4), new SolidBrush(Color.Black), 2 * CellWidth, 6 * CellHeight + CellHeight / 2 + 4);
        }
    }
    void DrawPuyo(Graphics g, Color color, int px, int py)//ぷよの描画
    {
        g.FillEllipse(new SolidBrush(color), px + 1, py + 1, CellWidth - 2, CellHeight - 2);
        g.FillEllipse(new SolidBrush(Color.White), px + 1, py + 1 + 2, (CellWidth - 2) / 2, (CellHeight - 2) / 2);
        g.FillEllipse(new SolidBrush(Color.White), px + 1 + CellWidth / 2, py + 1 + 2, (CellWidth - 2) / 2, (CellHeight - 2) / 2);
        g.FillEllipse(new SolidBrush(color), px + 1 + CellWidth / 4 - 1, py + 1 + 4, (CellWidth - 2) / 4, (CellHeight - 2) / 4);
        g.FillEllipse(new SolidBrush(color), px + 1 + CellWidth / 2 + 1, py + 1 + 4, (CellWidth - 2) / 4, (CellHeight - 2) / 4);
    }
}