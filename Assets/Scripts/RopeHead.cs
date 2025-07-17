using UnityEngine;

public class RopeHead : MonoBehaviour
{
    public float ropeLength = 2f;           //ぶら下がりのロープの長さ
    public float returnSpeed = 20f;          // 戻る速さ
    public float returnDelay = 0.2f;         // ブロックに当たらなければ戻るまでの時間
    float timer = 0f;　//投擲後に何秒たったか
    bool returning = false; //戻るかどうかのフラグ

    Rigidbody2D rb;

    //プレイヤーの情報の扱い
    Rigidbody2D playerRb;
    PlayerController playerController;


    void Start()
    {
        rb = GetComponent<Rigidbody2D>();        
        rb.gravityScale = 0f; // 重力なしで飛ばす
        timer = 0f; 
    }

    void Update()
    {
        timer += Time.deltaTime; //ロープが存在してから時間を測る

        // タイムアウトで戻るフラグON
        if (!returning && timer >= returnDelay)　returning = true;

        // 戻るフラグが立っていれば
        if (returning)
        {
            //プレイヤーの方向を取得し、そちらに加速させる
            Vector2 dirToPlayer = (playerRb.position - rb.position).normalized;
            rb.velocity = dirToPlayer * returnSpeed;

            // プレイヤーとの一定距離まで戻ったら破棄
            if (Vector2.Distance(rb.position, playerRb.position) < 0.5f)
            {
                // プレイヤーに戻ったことを通知
                playerController.OnRopeReturned();
                Destroy(gameObject);
                return; // 以降の処理を防ぐ
            }
        }
    }

    //ロープ生成じPlayerControllerによって呼び出される
    //プレイヤー情報(Rigidbody、PlayerControllerスクリプト)を取得する
    public void Init(Rigidbody2D player, PlayerController controller)
    {
        playerRb = player;
        playerController = controller;
    }

    //ホックとロープ先端がぶつかったら
    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("HookBlock")) return;

        // ロープ先端をHookBlockの中心に固定
        transform.position = other.transform.position;

        rb.velocity = Vector2.zero; //勢いを止める
        rb.bodyType = RigidbodyType2D.Static;  // ロープ先端を動かなくして完全に固定
        returning = false;  // 戻り状態だった場合はそれを中止

        // 既存のJointがあれば削除しておく
        var oldJoint = playerRb.GetComponent<SpringJoint2D>();
        if (oldJoint != null)
        {
            Destroy(oldJoint);
        }

        // SpringJoint2DコンポーネントをPlayerに追加してロープ先端とPlayerを繋ぐ
        var springJoint = playerRb.gameObject.AddComponent<SpringJoint2D>();
        springJoint.connectedBody = this.rb; //自分とPlayerをコネクト
        springJoint.autoConfigureDistance = false; //まずはAutoを切って手動設定を可にする
        springJoint.distance = ropeLength; //2つのオブジェクト間の距離
        springJoint.dampingRatio = 0.7f; //揺れの収まり安さ 0は減衰せず揺れる、1は速やかに揺れが収まる
        springJoint.frequency = 2f; //バネの硬さや反応の早さ 大きいとピンと張る、小さいとフワッとする
        springJoint.enableCollision = false; //接続された2者間の衝突判定
        springJoint.enabled = true; //このJoint設定における物理挙動を有効にするかどうか

        // プレイヤーのぶら下がり状態ON
        if (playerController != null) playerController.SetHanging(true);
        
        Destroy(this); // RopeHeadスクリプト自体はこれでもう不要なので削除
    }
}
