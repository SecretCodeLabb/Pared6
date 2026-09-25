using System;
using UnityEngine;

[Serializable]
public class PlayerState
{
    public CharacterData characterData;
    public int currentLives;
    public int damageBonus;
    public int defenseBonus;

    public int CurrentDamage => characterData.baseDamage + damageBonus;
    public int CurrentDefense => characterData.baseDefense + defenseBonus;
    public bool IsAlive => currentLives > 0;

    public PlayerState(CharacterData data)
    {
        characterData = data;
        currentLives = data.maxLives;
        damageBonus = 0;
        defenseBonus = 0;
    }

    public void TakeDamage(int damage)
    {
        currentLives -= damage;
        if (currentLives < 0) currentLives = 0;
    }

    public void AddDamageBonus(int amount)
    {
        damageBonus += amount;
    }

    public void AddDefenseBonus(int amount)
    {
        defenseBonus += amount;
    }
}
