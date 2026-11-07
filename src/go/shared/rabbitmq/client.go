package rabbitmq

import (
	"context"
	"encoding/json"
	"fmt"
	"log/slog"
	"sync"
	"time"

	amqp "github.com/rabbitmq/amqp091-go"
)

// Client quản lý kết nối và channel RabbitMQ với cơ chế auto-reconnect
type Client struct {
	url       string
	logger    *slog.Logger
	conn      *amqp.Connection
	ch        *amqp.Channel
	mu        sync.RWMutex
	isClosing bool
	done      chan struct{}
}

// NewClient khởi tạo RabbitMQ client
func NewClient(url string, logger *slog.Logger) (*Client, error) {
	if logger == nil {
		logger = slog.Default()
	}

	c := &Client{
		url:    url,
		logger: logger,
		done:   make(chan struct{}),
	}

	if err := c.connect(); err != nil {
		return nil, fmt.Errorf("không thể kết nối RabbitMQ (%s): %w", url, err)
	}

	go c.reconnectLoop()

	return c, nil
}

func (c *Client) connect() error {
	c.mu.Lock()
	defer c.mu.Unlock()

	conn, err := amqp.Dial(c.url)
	if err != nil {
		return err
	}

	ch, err := conn.Channel()
	if err != nil {
		_ = conn.Close()
		return err
	}

	c.conn = conn
	c.ch = ch
	c.logger.Info("RabbitMQ đã kết nối thành công")
	return nil
}

func (c *Client) reconnectLoop() {
	for {
		c.mu.RLock()
		if c.isClosing {
			c.mu.RUnlock()
			return
		}
		conn := c.conn
		c.mu.RUnlock()

		if conn != nil {
			closeErr := <-conn.NotifyClose(make(chan *amqp.Error, 1))
			if closeErr != nil {
				c.logger.Warn("Kết nối RabbitMQ bị ngắt, đang thử kết nối lại...", slog.Any("error", closeErr))
			}
		}

		select {
		case <-c.done:
			return
		case <-time.After(3 * time.Second):
			if err := c.connect(); err != nil {
				c.logger.Warn("Thử kết nối lại RabbitMQ thất bại, sẽ thử lại sau 3s", slog.Any("error", err))
			}
		}
	}
}

// PublishJSON gửi payload JSON tới exchange với routing key tương ứng (thread-safe)
func (c *Client) PublishJSON(ctx context.Context, exchange, routingKey string, payload any) error {
	c.mu.Lock()
	defer c.mu.Unlock()

	if c.ch == nil {
		return fmt.Errorf("kênh RabbitMQ chưa sẵn sàng")
	}

	if exchange != "" {
		_ = c.ch.ExchangeDeclare(
			exchange,
			"fanout",
			true,  // durable
			false, // auto-deleted
			false, // internal
			false, // no-wait
			nil,
		)
	}

	body, err := json.Marshal(payload)
	if err != nil {
		return fmt.Errorf("không thể marshal payload sang JSON: %w", err)
	}

	msg := amqp.Publishing{
		ContentType:  "application/json",
		DeliveryMode: amqp.Persistent,
		Timestamp:    time.Now().UTC(),
		Body:         body,
	}

	return c.ch.PublishWithContext(ctx, exchange, routingKey, false, false, msg)
}

// Subscribe đăng ký lắng nghe message từ một queue và bind tới exchange
func (c *Client) Subscribe(
	ctx context.Context,
	queueName, exchange, routingKey string,
	handler func(d amqp.Delivery),
) error {
	c.mu.Lock()
	ch := c.ch
	c.mu.Unlock()

	if ch == nil {
		return fmt.Errorf("kênh RabbitMQ chưa sẵn sàng")
	}

	if exchange != "" {
		_ = ch.ExchangeDeclare(
			exchange,
			"fanout",
			true,
			false,
			false,
			false,
			nil,
		)
	}

	q, err := ch.QueueDeclare(
		queueName,
		true,  // durable
		false, // auto-delete
		false, // exclusive
		false, // no-wait
		nil,
	)
	if err != nil {
		return fmt.Errorf("lỗi khai báo queue %s: %w", queueName, err)
	}

	if exchange != "" {
		if err := ch.QueueBind(q.Name, routingKey, exchange, false, nil); err != nil {
			return fmt.Errorf("lỗi bind queue %s tới exchange %s: %w", q.Name, exchange, err)
		}
	}

	msgs, err := ch.Consume(
		q.Name,
		"",    // consumer tag
		false, // auto-ack
		false, // exclusive
		false, // no-local
		false, // no-wait
		nil,
	)
	if err != nil {
		return fmt.Errorf("lỗi đăng ký consume từ queue %s: %w", q.Name, err)
	}

	go func() {
		for {
			select {
			case <-ctx.Done():
				return
			case d, ok := <-msgs:
				if !ok {
					return
				}
				handler(d)
			}
		}
	}()

	return nil
}

// Close đóng kết nối an toàn
func (c *Client) Close() error {
	c.mu.Lock()
	defer c.mu.Unlock()

	c.isClosing = true
	close(c.done)

	if c.ch != nil {
		_ = c.ch.Close()
	}
	if c.conn != nil {
		return c.conn.Close()
	}
	return nil
}

