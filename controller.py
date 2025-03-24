import socket
import RPi.GPIO as GPIO
from sense_hat import SenseHat
from threading import Thread

# Set up GPIO
GPIO.setmode(GPIO.BCM)
LED_PIN = 18
GPIO.setup(LED_PIN, GPIO.OUT)

# Set up Sense HAT
sense = SenseHat()

def udp_discovery_server():
    udp_socket = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    udp_socket.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
    udp_socket.bind(('0.0.0.0', 5000))  # Listen on port 5000 for UDP

    print("UDP discovery server started...")

    while True:
        data, address = udp_socket.recvfrom(1024)
        if data.decode() == "DISCOVER_RASPBERRY_PI":
            print(f"Discovery request from {address}")
            udp_socket.sendto("RASPBERRY_PI_RESPONSE".encode(), address)

def tcp_command_server():
    tcp_socket = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    tcp_socket.bind(('0.0.0.0', 5001))  # Listen on port 5001 for TCP
    tcp_socket.listen(5)  # Allow multiple connections

    print("TCP command server started...")

    while True:
        connection, address = tcp_socket.accept()
        print(f"Connected to {address}")

        Thread(target=handle_client, args=(connection,)).start()

def handle_client(connection):
    try:
        while True:
            data = connection.recv(1024).decode()
            if not data:
                break

            print(f"Received: {data}")

            if data == "LED_ON":
                GPIO.output(LED_PIN, GPIO.HIGH)
                sense.clear((0, 255, 0))  # Turn on Sense HAT LED matrix to green
                sense.show_message("Connected", scroll_speed=0.05, text_colour=[255, 255, 255])
                connection.send("LED turned ON".encode())
            elif data == "LED_OFF":
                GPIO.output(LED_PIN, GPIO.LOW)
                sense.clear()  # Turn off Sense HAT LED matrix
                connection.send("LED turned OFF".encode())
            else:
                connection.send("Unknown command!".encode())
    except ConnectionResetError:
        print("Client disconnected.")
    finally:
        connection.close()

# Start UDP and TCP servers in separate threads
Thread(target=udp_discovery_server).start()
Thread(target=tcp_command_server).start()
