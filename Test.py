from datetime import date, timedelta

MASK_64 = (1 << 64) - 1
HASH_XOR = 0xA98F501BC684032F


def stable_hash(value: str) -> int:
    """严格复刻 JS 中的 stableHash，返回值是 64 位无符号整数。"""
    result = 5381
    for ch in value:
        result = (((result << 5) ^ result ^ ord(ch)) & MASK_64)
    return result ^ HASH_XOR


def round_even(value: float) -> int:
    """严格复刻 JS 中的 roundEven 银行家舍入。"""
    lower = int(value)
    fraction = value - lower

    if fraction < 0.5:
        return lower
    if fraction > 0.5:
        return lower + 1

    # fraction == 0.5
    return lower if lower % 2 == 0 else lower + 1


def score_for_date(d: date, identifier: str) -> int:
    """完全复刻 JS 中的 scoreForDate。"""
    year = d.year
    day_of_year = d.timetuple().tm_yday
    day = d.day

    first_seed = f"asdfgbn{day_of_year}12#3$45{year}IUY"
    second_seed = f"QWERTY{identifier}0*8&6{day}kjhg"

    first_hash = stable_hash(first_seed) / 3
    second_hash = stable_hash(second_seed) / 3

    raw = abs((first_hash + second_hash) / 527) % 1001
    rounded = round_even(raw)

    if rounded >= 970:
        return 100

    return round_even((rounded / 969) * 99)


def main():
    identifier = input("请输入识别码：").strip()
    date_str = input("请输入日期，格式 YYYY-MM-DD：").strip()

    y, m, d = map(int, date_str.split("-"))
    target_date = date(y, m, d)

    print("\n单日校验：")
    print(f"识别码：{identifier}")
    print(f"日期：{target_date}")
    print(f"今日人品：{score_for_date(target_date, identifier)}")

    print("\n未来 365 天结果：")
    for i in range(365):
        cur = target_date + timedelta(days=i)
        score = score_for_date(cur, identifier)
        mark = "  <-- 100分" if score == 100 else ""
        print(f"{cur}  星期{cur.weekday() + 1}  分数：{score:3d}{mark}")


if __name__ == "__main__":
    main()
