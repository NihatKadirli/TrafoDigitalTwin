#!/usr/bin/env python3
"""
TRAFO MERKEZI MQTT SIMULATORU

Bu dosya lokal egitim/simulasyon icindir. Gercek sistemlere baglanmaz.
Ana telemetri topic'i korunur: TM1/Trafo/sensor

False Temperature Injection ek topic'leri:
- substation/attack/temperature/set      START | STOP | TOGGLE
- substation/transformer/displayed_temperature
- substation/transformer/real_temperature
"""

import json
import random
import sys
import threading
import time

import paho.mqtt.client as mqtt


BROKER = "localhost"
PORT = 1883
SENSOR_TOPIC = "TM1/Trafo/sensor"
ATTACK_COMMAND_TOPIC = "substation/attack/temperature/set"
DISPLAYED_TEMPERATURE_TOPIC = "substation/transformer/displayed_temperature"
REAL_TEMPERATURE_TOPIC = "substation/transformer/real_temperature"

NORMAL_TEMPERATURE = 58.0
HEATING_RATE = 3.0
COOLING_RATE = 2.0
LOOP_SECONDS = 2

false_temperature_attack_active = False
real_temperature = NORMAL_TEMPERATURE
displayed_temperature = NORMAL_TEMPERATURE
running = True
state_lock = threading.Lock()


def get_status(temp: float) -> str:
    if temp > 110:
        return "FAILURE"
    if temp > 100:
        return "SMOKE"
    if temp > 85:
        return "CRITICAL"
    if temp > 70:
        return "WARNING"
    return "NORMAL"


def set_false_temperature_attack(active: bool) -> None:
    global false_temperature_attack_active
    with state_lock:
        if false_temperature_attack_active == active:
            return
        false_temperature_attack_active = active

    if active:
        print("[ATTACK STARTED] False Temperature Data Injection")
    else:
        print("[ATTACK STOPPED] Temperature data restored")


def on_connect(client: mqtt.Client, _userdata, _flags, rc):
    if rc == 0:
        print("MQTT broker'a baglandi.")
        client.subscribe(ATTACK_COMMAND_TOPIC)
        print(f"Attack command topic dinleniyor: {ATTACK_COMMAND_TOPIC}")
    else:
        print(f"MQTT baglanti kodu: {rc}")


def on_message(_client: mqtt.Client, _userdata, message) -> None:
    payload = message.payload.decode("utf-8", errors="ignore").strip().lower()
    if message.topic != ATTACK_COMMAND_TOPIC:
        return

    if payload in {"start", "on", "1", "true"}:
        set_false_temperature_attack(True)
    elif payload in {"stop", "off", "0", "false", "none"}:
        set_false_temperature_attack(False)
    elif payload == "toggle":
        with state_lock:
            next_state = not false_temperature_attack_active
        set_false_temperature_attack(next_state)
    else:
        print(f"Bilinmeyen attack command payload: {payload}")


def terminal_input_loop() -> None:
    print("")
    print("Komutlar: attack on | attack off | q")
    while running:
        try:
            command = input().strip().lower()
        except EOFError:
            return

        if command in {"q", "quit", "exit"}:
            print("Cikis komutu alindi. Ctrl+C ile de durdurabilirsiniz.")
            return
        if command in {"attack on", "on", "start"}:
            set_false_temperature_attack(True)
        elif command in {"attack off", "off", "stop"}:
            set_false_temperature_attack(False)
        elif command:
            print("Kullanim: attack on | attack off | q")


def update_temperatures() -> tuple[float, float, bool, str]:
    global real_temperature, displayed_temperature

    with state_lock:
        attack_active = false_temperature_attack_active

    if attack_active:
        real_temperature = min(118.0, real_temperature + random.uniform(HEATING_RATE * 0.7, HEATING_RATE * 1.3))
        displayed_temperature = random.uniform(55.0, 60.0)
    else:
        ambient_target = random.uniform(NORMAL_TEMPERATURE - 2.0, NORMAL_TEMPERATURE + 2.0)
        if real_temperature > ambient_target:
            real_temperature = max(ambient_target, real_temperature - random.uniform(COOLING_RATE * 0.7, COOLING_RATE * 1.3))
        else:
            real_temperature = min(ambient_target, real_temperature + random.uniform(0.2, 0.8))
        displayed_temperature = real_temperature

    status = get_status(real_temperature)
    return real_temperature, displayed_temperature, attack_active, status


def build_payload() -> dict:
    real_temp, displayed_temp, attack_active, status = update_temperatures()

    return {
        # Geriye uyumluluk: Unity/SCADA'nin mevcut sicaklik alani sahte/gorunen degeri tasir.
        "sicaklik": round(displayed_temp, 2),
        "yag_sicaklik": round(max(30.0, real_temp - random.uniform(8.0, 15.0)), 2),
        "gerilim_primer": round(random.uniform(154000, 160000), 1),
        "gerilim_sekonder": round(random.uniform(33000, 36000), 1),
        "akim_primer": round(random.uniform(100, 550), 1),
        "akim_sekonder": round(random.uniform(800, 2600), 1),
        "yuk_seviyesi": round(random.uniform(60, 95), 1),
        "realTemperature": round(real_temp, 2),
        "displayedTemperature": round(displayed_temp, 2),
        "attackActive": attack_active,
        "attackType": "False Temperature Injection" if attack_active else "None",
        "status": status,
    }


def publish_payload(client: mqtt.Client, payload: dict) -> None:
    try:
        client.publish(SENSOR_TOPIC, json.dumps(payload))
        client.publish(DISPLAYED_TEMPERATURE_TOPIC, str(payload["displayedTemperature"]))
        client.publish(REAL_TEMPERATURE_TOPIC, str(payload["realTemperature"]))
    except Exception as exc:
        print(f"MQTT publish hatasi: {exc}")


def main() -> None:
    global running

    print("=" * 60)
    print("TRAFO MERKEZI MQTT SIMULATORU")
    print("=" * 60)
    print(f"Broker: {BROKER}:{PORT}")
    print(f"Telemetry topic: {SENSOR_TOPIC}")

    client = mqtt.Client()
    client.on_connect = on_connect
    client.on_message = on_message

    try:
        client.connect(BROKER, PORT, keepalive=60)
    except Exception as exc:
        print(f"MQTT baglanti hatasi: {exc}")
        print("Mosquitto broker'in localhost:1883 uzerinde calistigindan emin olun.")
        sys.exit(1)

    client.loop_start()

    input_thread = threading.Thread(target=terminal_input_loop, daemon=True)
    input_thread.start()

    counter = 0
    try:
        while True:
            payload = build_payload()
            publish_payload(client, payload)
            counter += 1

            attack_label = "AKTIF" if payload["attackActive"] else "NORMAL"
            print(f"{counter}. Veri gonderildi | Attack: {attack_label} | Status: {payload['status']}")
            print(f"   displayedTemperature: {payload['displayedTemperature']:.1f} C")
            print(f"   realTemperature     : {payload['realTemperature']:.1f} C")
            print(f"   gerilim             : {payload['gerilim_primer'] / 1000:.1f} kV")
            print("-" * 40)

            time.sleep(LOOP_SECONDS)
    except KeyboardInterrupt:
        print("\nSimulasyon durduruldu.")
    finally:
        running = False
        client.loop_stop()
        client.disconnect()
        print("MQTT baglantisi kapatildi.")


if __name__ == "__main__":
    main()
