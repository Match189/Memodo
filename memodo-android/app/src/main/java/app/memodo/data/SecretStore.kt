package app.memodo.data

import android.content.Context
import android.security.keystore.KeyGenParameterSpec
import android.security.keystore.KeyProperties
import android.util.Base64
import java.security.KeyStore
import javax.crypto.Cipher
import javax.crypto.KeyGenerator
import javax.crypto.SecretKey
import javax.crypto.spec.GCMParameterSpec

/**
 * 凭据加密存储（对齐 Windows 端 DPAPI 水平）：
 * AndroidKeyStore 生成不可导出的 AES-256 主密钥，AES-GCM 加密后落 SharedPreferences。
 * 明文密码不出现在磁盘。WebDAV/服务器账号密码首次读取旧明文时自动迁移。
 */
object SecretStore {
    private const val KEYSTORE = "AndroidKeyStore"
    private const val ALIAS = "memodo_secret_main"
    private const val PREFS = "secret_store"

    private fun prefs(ctx: Context) =
        ctx.getSharedPreferences(PREFS, Context.MODE_PRIVATE)

    /** 取（或首次生成）KeyStore 内的 AES 主密钥。 */
    private fun mainKey(): SecretKey {
        val ks = KeyStore.getInstance(KEYSTORE).apply { load(null) }
        (ks.getKey(ALIAS, null) as? SecretKey)?.let { return it }
        val kg = KeyGenerator.getInstance(KeyProperties.KEY_ALGORITHM_AES, KEYSTORE)
        kg.init(KeyGenParameterSpec.Builder(ALIAS,
            KeyProperties.PURPOSE_ENCRYPT or KeyProperties.PURPOSE_DECRYPT)
            .setBlockModes(KeyProperties.BLOCK_MODE_GCM)
            .setEncryptionPaddings(KeyProperties.ENCRYPTION_PADDING_NONE)
            .setKeySize(256)
            .build())
        return kg.generateKey()
    }

    /** 加密明文 → "iv:ct" base64。 */
    fun encrypt(plain: String): String {
        if (plain.isEmpty()) return ""
        val c = Cipher.getInstance("AES/GCM/NoPadding")
        c.init(Cipher.ENCRYPT_MODE, mainKey())
        val ct = c.doFinal(plain.toByteArray(Charsets.UTF_8))
        return Base64.encodeToString(c.iv, Base64.NO_WRAP) + ":" +
                Base64.encodeToString(ct, Base64.NO_WRAP)
    }

    /** 解密 "iv:ct" base64；格式不符返回 null（调用方按旧明文迁移处理）。 */
    fun decrypt(blob: String): String? {
        if (blob.isEmpty()) return ""
        val parts = blob.split(":", limit = 2)
        if (parts.size != 2) return null
        return try {
            val iv = Base64.decode(parts[0], Base64.NO_WRAP)
            val ct = Base64.decode(parts[1], Base64.NO_WRAP)
            val c = Cipher.getInstance("AES/GCM/NoPadding")
            c.init(Cipher.DECRYPT_MODE, mainKey(), GCMParameterSpec(128, iv))
            String(c.doFinal(ct), Charsets.UTF_8)
        } catch (e: Exception) { null }
    }

    /** 读：密文解密；兼容旧明文（自动迁移为密文）。 */
    fun get(ctx: Context, key: String): String {
        val sp = prefs(ctx)
        val stored = sp.getString(key, "") ?: ""
        if (stored.isEmpty()) return ""
        val plain = decrypt(stored)
        if (plain != null) return plain
        // 旧明文数据（无 "iv:ct" 结构）→ 迁移
        sp.edit().putString(key, encrypt(stored)).apply()
        return stored
    }

    /** 写：总是密文落盘。 */
    fun put(ctx: Context, key: String, plain: String) {
        prefs(ctx).edit().putString(key, if (plain.isEmpty()) "" else encrypt(plain)).apply()
    }
}
