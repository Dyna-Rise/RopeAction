using UnityEngine;
using UnityEngine.InputSystem; //PackegeManagerからInputSystemパッケージの導入が必要

public class PlayerController : MonoBehaviour
{
    [Header("移動・ジャンプ設定")]
    public float moveSpeed = 5f; //移動スピード
    public float jumpForce = 7f; //ジャンプ力
    public LayerMask groundLayer; //地面判定
    public Transform groundCheck; //地面判定をするオブジェクト（子オブジェクト）の指定
    public float groundCheckRadius = 0.2f; //地面判定の半径
    bool isFacingRight = true;  // 初期は右向きのフラグ
    bool isGrounded = false;

    [Header("ロープ設定")]
    public GameObject ropeHeadPrefab; //生成するロープの先端オブジェクト
    public float ropeSpeed = 15f; //ロープの投擲スピ―ド
    public float ropeLength = 2f; //ロープの長さ
    bool ropeShot = false; //ロープ投擲中か
    bool isHanging = false; //ぶら下がり中か
    public float hangGravity = 10f; //ぶら下がり中の重力
    float normalGravity = 1f; //通常の重力
    GameObject currentRopeHead = null; //射出中のロープ情報

    [Header("ロープの縄部分")]
    public LineRenderer ropeRenderer;  // RopeVisualをInspectorで指定

    Rigidbody2D rb;
    Animator anime;

    [Header("コントローラー設定")]    
    public PlayerControls controls; // InputaAtions設定より自動生成されたクラスをアタッチ
    Vector2 moveInput;
    bool jumpPressed;
    bool ropeShootPressed;

    // スウィング強化用
    int previousInput = 0;// ひとつ前の入力方向を記録（-1:左, 1:右）
    int swingCount = 0;           // 往復回数
    float lastSwingTime = 0f;     // 最後に入力が切り替わった時間
    float swingDecayTime = 1.0f;  // 入力しなかったらカウントをリセットする時間
     float baseSwingForce = 2f;
    float swingForceMultiplier = 0.5f;  // カウントに応じた力の増加倍率


    void Awake()
    {
        //新しいInputシステムのマッピングと連動

        controls = new PlayerControls(); //コントローラークラスの実体化

        // 移動入力
        //Moveアクションのいずれかがおされた時(performed)、アクションをやめた時(canceled)
        //ctxはCallbackContext 型の引数で無名関数、ReadValue<Vector2>()は入力を読み込む
        controls.Player.Move.performed += ctx => moveInput = ctx.ReadValue<Vector2>();
        controls.Player.Move.canceled += ctx => moveInput = Vector2.zero;

        // ジャンプアクションのいずれかがおされた時
        controls.Player.Jump.performed += ctx => jumpPressed = true;

        // ロープアクションのいずれかが押された時
        controls.Player.Rope.performed += ctx => ropeShootPressed = true;
    }


    //次の2つはいずれもエントリーポイントの一種
    //オブジェクトが有効化された時
    //Input Systemは明示的に有効化（Enable()）しないと動作しない
    void OnEnable() => controls.Enable(); //メソッド定義：マッピングで決めた入力受付を有効
 
    //オブジェクトが無効化された時
    void OnDisable() => controls.Disable();//メソッド定義：マッピングで決めた入力受付も無効

    void Start()
    {
        anime = GetComponent<Animator>();

        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = normalGravity;
        rb.angularDrag = 3f;  // 減衰の強さを調整
    }

    void Update()
    {
        // 地面判定
        isGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);

        // 重力切り替え（ぶら下がり中は専用のGravityScale値に）
        rb.gravityScale = isHanging ? hangGravity : normalGravity;

        HandleMovement(); //移動受付
        HandleSwingInput(); //ぶら下がり中の移動受付
        HandleJump(); //ジャンプ受付
        HandleRope(); //ロープ投げ

        // ジャンプとロープのフラグを1フレームごとにリセットしておく
        //これをやらないと一度発動→連続発生になる
        jumpPressed = false;
        ropeShootPressed = false;
    }

    void LateUpdate()
    {
        UpdateRopeVisual(); //ロープ射出中に途中の縄部分（LineRenderer）を描画する
    }

    // --- 入力値獲得のメソッド群 ---
    float GetHorizontalInput() => moveInput.x; //移動入力値を取得
    bool IsJumpPressed() => jumpPressed; //ジャンプボタン値を取得
    bool IsRopeShootPressed() => ropeShootPressed; //ロープ射出ボタン値を取得

    //移動メソッド
    void HandleMovement()
    {
        float h = GetHorizontalInput(); //移動入力値を取得

        // スプライト反転
        Vector3 scale = transform.localScale;
        scale.x = isFacingRight ? 1 : -1;
        transform.localScale = scale;

        // 向きフラグを更新
        if (h > 0) isFacingRight = true;
        else if (h < 0) isFacingRight = false;

        //アニメーション切り替え：入力あればRunアニメ、そうでなければIdleアニメ
        if (h != 0f) anime.SetBool("run", true);
        else anime.SetBool("run", false);

        //ぶら下がり中ならvelocityを触らない ※向きフラグだけに留めるということ
        if (!isHanging)　rb.velocity = new Vector2(h * moveSpeed, rb.velocity.y);
    }


    //ジャンプ
    void HandleJump()
    {
        if (IsJumpPressed()) //ジャンプボタンの受付があれば
        {
            if (isHanging) //ぶら下がり中なら
            {
                if (currentRopeHead == null) return; //ロープオブジェクトが無い時は何もしない

                Vector2 ropeHeadPosition = currentRopeHead.transform.position; //ロープ先端のポジション
                Vector2 ropeDir = (ropeHeadPosition - rb.position).normalized; //ロープの伸びている方向

                //引数A、Bのベクトルの内積 （結果が正：Velocityが同じ方向を向いている、0:垂直、負：Velocityが違う方向を向いている）
                float swingSpeedAlongRope = Vector2.Dot(rb.velocity, ropeDir); 
                float swingBoost = Mathf.Max(0f, swingSpeedAlongRope) * 1f;  //大きい方の引数を採用

                // ぶら下がりジャンプ
                rb.velocity = new Vector2(rb.velocity.x, 0f);
                rb.AddForce(Vector2.up * (jumpForce + swingBoost), ForceMode2D.Impulse);

                ReleaseHang(); //DistanceJoint2Dコンポーネントを破棄するメソッド
            }
            else if (isGrounded) //地面にいれば
            {                
                rb.velocity = new Vector2(rb.velocity.x, jumpForce);// 地面ジャンプする
                anime.SetTrigger("jump"); //ジャンプアニメ
            }
        }
    }

    //ロープ投げ
    void HandleRope()
    {
        //ロープボタンが押されて、ロープ投げ中でなければ
        if (IsRopeShootPressed() && !ropeShot)
        {
            ropeShot = true; //ロープ投げ中フラグを立てる（連続投擲できない）

            // 入力方向ベクトル（斜め・真上も含めて）
            float h = moveInput.x;
            float v = moveInput.y;
            Vector2 inputDir = new Vector2(h, v);

            // 入力がない場合、前向きに撃つ（右か左）
            if (inputDir == Vector2.zero)
                inputDir = isFacingRight ? Vector2.right : Vector2.left;

            inputDir.Normalize(); // 決まった方向を正規化

            Vector3 spawnPos = transform.position + (Vector3)(inputDir * 0.5f); //プレイヤーより少し前を指定
            GameObject rope = Instantiate(ropeHeadPrefab, spawnPos, Quaternion.identity);//ロープ先端の生成
            currentRopeHead = rope; //現在のロープ情報を変数に格納

            //生成したロープ先端のRigidboodyとスクリプトを取得
            Rigidbody2D ropeRb = rope.GetComponent<Rigidbody2D>();
            RopeHead ropeHead = rope.GetComponent<RopeHead>();
            ropeHead.Init(rb, this); //生成したロープ先端のスクリプトの変数にPlayer情報を伝えておく
            ropeHead.ropeLength = ropeLength; //ロープの長さ設定を共有
            ropeRb.velocity = inputDir * ropeSpeed; //生成したロープ先端を決めてあった方向に飛ばす
        }
    }

    //LateUpdateでロープ先端とPlayerの間の縄を描画する
    void UpdateRopeVisual()
    {
        //Updateにて、ロープが発射されていて、かつまだ未描画であれば
        if (currentRopeHead != null && ropeRenderer != null)
        {
            ropeRenderer.enabled = true;  //PlayerについているRopeVisualのLineRendererを有効
            ropeRenderer.positionCount = 2; //ポジションは2点の情報
            ropeRenderer.SetPosition(0, transform.position);                 // プレイヤー側から
            ropeRenderer.SetPosition(1, currentRopeHead.transform.position); // ロープ先端まで
        }
        else
        {
            ropeRenderer.enabled = false; //条件達成していなければLineRendererは無効
        }
    }

    //ぶら下がり中
    void HandleSwingInput()
    {
        if (!isHanging) return;

        //左右の入力を変数hで取得（HandleMovementメソッドとは別途取得）
        float h = GetHorizontalInput();

        int currentInput = (int)Mathf.Sign(h); //引数hの符号がプラスかマイナスかを調べて1か-1かに整理

        // 左右に切り返した場合だけ swingCount++
        //previousInput：ひとつ前のフレームの状態が入っている1、0、-1かのいずれか
        if (currentInput != 0 && previousInput == -currentInput)
        {
            swingCount++; //振り子の往復回数
            lastSwingTime = Time.time; //その時のゲーム時間を利用
        }

        if (currentInput != 0) //振り子カウントの分だけ左右に振る力が増していく
        {
            float swingForce = baseSwingForce + swingCount * swingForceMultiplier;
            rb.AddForce(new Vector2(currentInput * swingForce, 0f));
        }

        previousInput = currentInput; //現在の入力(1,0,-1)を記録して次フレームに備える

        // 一定時間スイングが止まったら振り子回数をリセット
        if (Time.time - lastSwingTime > swingDecayTime)
        {
            swingCount = 0;
        }
    }

    //RoleHead.cs側からロープがPlayerに戻ったと判断したら呼び出される
    public void OnRopeReturned()
    {
        ropeShot = false; //次のロープが打てるようにフラグをOFF
        currentRopeHead = null; //ロープ情報をなし
        isHanging = false; //ぶら下がりフラグもOFF
    }

    //RoleHead.cs側からロープがHockに引っかかったら呼び出される
    public void SetHanging(bool hanging)
    {
        isHanging = hanging;
    }

    //ぶら下がり中にジャンプした際に呼び出される
    public void ReleaseHang()
    {
        // ジョイント破棄
        var joint = GetComponent<DistanceJoint2D>();
        if (joint != null)
        {
            Destroy(joint);
        }

        // ロープ先端破棄
        if (currentRopeHead != null)
        {
            Destroy(currentRopeHead);
            currentRopeHead = null;
        }

        isHanging = false; //ぶら下がりフラグOFF
        ropeShot = false; //ロープ投擲中フラグOFF
    }
}
