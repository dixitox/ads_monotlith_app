// Side Chat Panel functionality
(function () {
    'use strict';

    const toggleBtn = document.getElementById('toggleSideChat');
    const closeBtn = document.getElementById('closeSideChat');
    const overlay = document.getElementById('sideChatOverlay');
    const panel = document.getElementById('sideChatPanel');
    const chatForm = document.getElementById('sideChatForm');
    const chatInput = document.getElementById('sideChatInput');
    const chatContainer = document.getElementById('sideChatContainer');
    const sendButton = document.getElementById('sideChatSend');
    const pinBtn = document.getElementById('pinSideChat');

    // Store conversation history (load from sessionStorage if available)
    let conversationHistory = loadConversationHistory();
    let isPinned = loadPinnedState();
    let totalTokensUsed = loadTokenCount();

    // Restore chat messages on page load
    restoreChatMessages();
    
    // Initialize token display
    updateTokenDisplay();
    
    // Restore pinned state on page load
    if (isPinned) {
        panel.classList.add('open');
        overlay.classList.add('show', 'pinned');
        document.body.style.overflow = ''; // Allow scrolling when pinned
    }

    // Initialize event listeners
    if (toggleBtn) {
        toggleBtn.addEventListener('click', openChat);
    }

    if (closeBtn) {
        closeBtn.addEventListener('click', closeChat);
    }

    if (overlay) {
        overlay.addEventListener('click', function() {
            if (!isPinned) {
                closeChat();
            }
        });
    }

    if (pinBtn) {
        pinBtn.addEventListener('click', togglePin);
        if (isPinned) {
            pinBtn.classList.add('pinned');
            pinBtn.querySelector('i').classList.remove('bi-pin');
            pinBtn.querySelector('i').classList.add('bi-pin-fill');
        }
    }

    if (chatForm) {
        chatForm.addEventListener('submit', handleSubmit);
    }

    // Clear chat button
    const clearBtn = document.getElementById('clearChatBtn');
    if (clearBtn) {
        clearBtn.addEventListener('click', clearChat);
    }

    // Prompt suggestion chips
    document.addEventListener('click', function(e) {
        if (e.target.classList.contains('prompt-chip')) {
            const promptText = e.target.dataset.prompt;
            if (promptText) {
                chatInput.value = promptText;
                chatInput.focus();
            }
        }
    });

    // Keyboard shortcuts
    document.addEventListener('keydown', function(e) {
        // ESC to close
        if (e.key === 'Escape' && panel.classList.contains('open')) {
            closeChat();
        }
    });

    function openChat() {
        panel.classList.add('open');
        overlay.classList.add('show');
        
        // Only disable body scroll if not pinned
        if (!isPinned) {
            document.body.style.overflow = 'hidden';
        }
        
        // Focus input after animation
        setTimeout(() => {
            chatInput.focus();
        }, 300);
    }

    function closeChat() {
        if (isPinned) return; // Don't close if pinned
        panel.classList.remove('open');
        overlay.classList.remove('show');
        document.body.style.overflow = '';
    }

    function togglePin() {
        isPinned = !isPinned;
        savePinnedState();
        
        const icon = pinBtn.querySelector('i');
        if (isPinned) {
            pinBtn.classList.add('pinned');
            pinBtn.title = 'Unpin chat';
            icon.classList.remove('bi-pin');
            icon.classList.add('bi-pin-fill');
            // Remove overlay and re-enable scrolling when pinned
            overlay.classList.add('pinned');
            document.body.style.overflow = '';
        } else {
            pinBtn.classList.remove('pinned');
            pinBtn.title = 'Pin chat (keeps open while browsing)';
            icon.classList.add('bi-pin');
            icon.classList.remove('bi-pin-fill');
            // Restore overlay when unpinned
            overlay.classList.remove('pinned');
            if (panel.classList.contains('open')) {
                document.body.style.overflow = 'hidden';
            }
        }
    }

    async function handleSubmit(e) {
        e.preventDefault();

        const message = chatInput.value.trim();
        if (!message) return;

        // Disable input while processing
        setInputState(false);

        // Add user message to UI
        addMessage(message, 'user');

        // Clear input
        chatInput.value = '';

        // Add typing indicator
        const typingIndicator = addTypingIndicator();

        try {
            // Get CSRF token if available
            const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;

            // Send request to API
            const response = await fetch('/api/chat', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    ...(token && { 'RequestVerificationToken': token })
                },
                body: JSON.stringify({
                    message: message,
                    conversationHistory: conversationHistory
                })
            });

            // Remove typing indicator
            removeTypingIndicator(typingIndicator);

            if (!response.ok) {
                throw new Error(`HTTP error! status: ${response.status}`);
            }

            const data = await response.json();

            // Add assistant response to UI
            addMessage(data.response, 'assistant');

            // Update conversation history
            conversationHistory.push({ role: 'user', content: message });
            conversationHistory.push({ role: 'assistant', content: data.response });

            // Keep only last 10 messages (5 exchanges) to manage context size
            if (conversationHistory.length > 10) {
                conversationHistory = conversationHistory.slice(-10);
            }

            // Track token usage (estimate: ~4 chars per token)
            const estimatedTokens = Math.ceil((message.length + data.response.length) / 4);
            totalTokensUsed += estimatedTokens;
            saveTokenCount();
            updateTokenDisplay();

            // Save conversation to sessionStorage
            saveConversationHistory();

        } catch (error) {
            console.error('Error sending message:', error);
            removeTypingIndicator(typingIndicator);
            addMessage('Sorry, I encountered an error. Please try again.', 'assistant', true);
        } finally {
            // Re-enable input
            setInputState(true);
            chatInput.focus();
        }
    }

    function addMessage(text, role, isError = false) {
        const messageDiv = document.createElement('div');
        messageDiv.className = `chat-message ${role}-message${isError ? ' error-message' : ''}`;

        const avatarDiv = document.createElement('div');
        avatarDiv.className = 'message-avatar';
        avatarDiv.innerHTML = role === 'user' 
            ? '<i class="bi bi-person-circle"></i>' 
            : '<i class="bi bi-robot"></i>';

        const bubbleDiv = document.createElement('div');
        bubbleDiv.className = 'message-bubble';
        
        // Convert markdown-style formatting to HTML and add interactivity
        bubbleDiv.innerHTML = formatMessage(text, role);
        
        // Add event listeners for interactive elements (only for assistant messages)
        if (role === 'assistant') {
            attachMessageInteractivity(bubbleDiv);
        }

        messageDiv.appendChild(avatarDiv);
        messageDiv.appendChild(bubbleDiv);

        chatContainer.appendChild(messageDiv);
        scrollToBottom();
    }

    function formatMessage(text, role = 'assistant') {
        // Basic markdown-like formatting
        let formatted = text
            // Convert line breaks
            .replace(/\n/g, '<br>')
            // Bold text **text**
            .replace(/\*\*(.*?)\*\*/g, '<strong>$1</strong>')
            // Italic text *text*
            .replace(/\*(.*?)\*/g, '<em>$1</em>')
            // Code blocks `code`
            .replace(/`(.*?)`/g, '<code>$1</code>');

        // For assistant messages, convert product references to clickable links
        if (role === 'assistant') {
            // Match product name with ID pattern: ProductName [PRODUCT:id:ProductName]
            // Remove target="_blank" to keep chat open
            formatted = formatted.replace(/\[PRODUCT:(\d+):([^\]]+)\]/g, '<a href="/Products/Details/$1" class="product-link" data-product-id="$1"><i class="bi bi-box-seam"></i> $2</a>');
            
            // Match simple product IDs [PRODUCT:id]
            formatted = formatted.replace(/\[PRODUCT:(\d+)\]/g, '<a href="/Products/Details/$1" class="product-link" data-product-id="$1"><i class="bi bi-box-seam"></i> View Product</a>');
            
            // Match place order with product name [PLACE_ORDER:id:ProductName]
            formatted = formatted.replace(/\[PLACE_ORDER:(\d+):([^\]]+)\]/g, '<button class="action-button place-order-btn" data-product-id="$1"><i class="bi bi-bag-check"></i>Order $2</button>');
            
            // Match place order actions [PLACE_ORDER:id]
            formatted = formatted.replace(/\[PLACE_ORDER:(\d+)\]/g, '<button class="action-button place-order-btn" data-product-id="$1"><i class="bi bi-bag-check"></i>Place Order</button>');
            
            // Match add to cart with product name [ADD_TO_CART:id:ProductName]
            formatted = formatted.replace(/\[ADD_TO_CART:(\d+):([^\]]+)\]/g, '<button class="action-button add-to-cart-btn" data-product-id="$1"><i class="bi bi-cart-plus"></i>Add $2</button>');
            
            // Match add to cart actions [ADD_TO_CART:id]
            formatted = formatted.replace(/\[ADD_TO_CART:(\d+)\]/g, '<button class="action-button add-to-cart-btn" data-product-id="$1"><i class="bi bi-cart-plus"></i>Add to Cart</button>');
            
            // Match bulk add all to cart [ADD_ALL_TO_CART:id1,id2,id3]
            formatted = formatted.replace(/\[ADD_ALL_TO_CART:([\d,]+)\]/g, '<button class="action-button add-all-to-cart-btn" data-product-ids="$1"><i class="bi bi-cart-plus-fill"></i>Add All to Cart</button>');
            
            // Match bulk order all [ORDER_ALL:id1,id2,id3]
            formatted = formatted.replace(/\[ORDER_ALL:([\d,]+)\]/g, '<button class="action-button order-all-btn" data-product-ids="$1"><i class="bi bi-bag-check-fill"></i>Order All</button>');
        }

        return formatted;
    }

    function attachMessageInteractivity(bubbleElement) {
        // Handle add to cart buttons
        const addToCartButtons = bubbleElement.querySelectorAll('.add-to-cart-btn');
        addToCartButtons.forEach(btn => {
            btn.addEventListener('click', async function(e) {
                e.preventDefault();
                const productId = this.dataset.productId;
                await handleAddToCart(productId, this);
            });
        });
        
        // Handle place order buttons
        const placeOrderButtons = bubbleElement.querySelectorAll('.place-order-btn');
        placeOrderButtons.forEach(btn => {
            btn.addEventListener('click', async function(e) {
                e.preventDefault();
                const productId = this.dataset.productId;
                await handlePlaceOrder(productId, this);
            });
        });
        
        // Handle add all to cart buttons
        const addAllButtons = bubbleElement.querySelectorAll('.add-all-to-cart-btn');
        addAllButtons.forEach(btn => {
            btn.addEventListener('click', async function(e) {
                e.preventDefault();
                const productIds = this.dataset.productIds;
                await handleAddAllToCart(productIds, this);
            });
        });
        
        // Handle order all buttons
        const orderAllButtons = bubbleElement.querySelectorAll('.order-all-btn');
        orderAllButtons.forEach(btn => {
            btn.addEventListener('click', async function(e) {
                e.preventDefault();
                const productIds = this.dataset.productIds;
                await handleOrderAll(productIds, this);
            });
        });
    }

    function addTypingIndicator() {
        const messageDiv = document.createElement('div');
        messageDiv.className = 'chat-message assistant-message typing-indicator';
        messageDiv.id = 'typingIndicator';

        const avatarDiv = document.createElement('div');
        avatarDiv.className = 'message-avatar';
        avatarDiv.innerHTML = '<i class="bi bi-robot"></i>';

        const dotsDiv = document.createElement('div');
        dotsDiv.className = 'typing-dots';
        dotsDiv.innerHTML = '<span></span><span></span><span></span>';

        messageDiv.appendChild(avatarDiv);
        messageDiv.appendChild(dotsDiv);

        chatContainer.appendChild(messageDiv);
        scrollToBottom();

        return messageDiv;
    }

    function removeTypingIndicator(indicator) {
        if (indicator && indicator.parentNode) {
            indicator.parentNode.removeChild(indicator);
        }
    }

    function setInputState(enabled) {
        chatInput.disabled = !enabled;
        sendButton.disabled = !enabled;
        
        if (enabled) {
            sendButton.innerHTML = '<i class="bi bi-send-fill"></i>';
        } else {
            sendButton.innerHTML = '<span class="spinner-border spinner-border-sm" role="status" aria-hidden="true"></span>';
        }
    }

    function scrollToBottom() {
        const body = document.querySelector('.side-chat-body');
        if (body) {
            body.scrollTop = body.scrollHeight;
        }
    }

    function saveConversationHistory() {
        try {
            sessionStorage.setItem('chatHistory', JSON.stringify(conversationHistory));
            
            // Also save the visible messages (without the welcome message)
            const messages = Array.from(chatContainer.querySelectorAll('.chat-message'))
                .filter(msg => !msg.querySelector('.message-bubble ul')) // Skip welcome message
                .map(msg => ({
                    role: msg.classList.contains('user-message') ? 'user' : 'assistant',
                    text: msg.querySelector('.message-bubble').innerHTML,
                    isError: msg.classList.contains('error-message')
                }));
            sessionStorage.setItem('chatMessages', JSON.stringify(messages));
        } catch (e) {
            console.error('Error saving conversation history:', e);
        }
    }

    function loadConversationHistory() {
        try {
            const saved = sessionStorage.getItem('chatHistory');
            return saved ? JSON.parse(saved) : [];
        } catch (e) {
            console.error('Error loading conversation history:', e);
            return [];
        }
    }

    function savePinnedState() {
        try {
            sessionStorage.setItem('chatPinned', JSON.stringify(isPinned));
        } catch (e) {
            console.error('Error saving pinned state:', e);
        }
    }

    function loadPinnedState() {
        try {
            const saved = sessionStorage.getItem('chatPinned');
            return saved ? JSON.parse(saved) : false;
        } catch (e) {
            console.error('Error loading pinned state:', e);
            return false;
        }
    }

    function saveTokenCount() {
        try {
            sessionStorage.setItem('chatTokens', JSON.stringify(totalTokensUsed));
        } catch (e) {
            console.error('Error saving token count:', e);
        }
    }

    function loadTokenCount() {
        try {
            const saved = sessionStorage.getItem('chatTokens');
            return saved ? JSON.parse(saved) : 0;
        } catch (e) {
            console.error('Error loading token count:', e);
            return 0;
        }
    }

    function updateTokenDisplay() {
        const tokenDisplay = document.getElementById('tokenCounter');
        if (tokenDisplay) {
            tokenDisplay.innerHTML = `<span class="tokens">${totalTokensUsed.toLocaleString()}</span> tokens used this session`;
        }
    }

    async function handleAddToCart(productId, buttonElement) {
        const originalHTML = buttonElement.innerHTML;
        buttonElement.disabled = true;
        buttonElement.innerHTML = '<span class="spinner-border spinner-border-sm" role="status"></span> Adding...';

        try {
            // Get customer ID from authenticated user
            const customerId = window.currentUserId || 'default-customer';
            
            const response = await fetch(`/api/cart/${customerId}/items?productId=${productId}&quantity=1`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                }
            });

            if (response.ok) {
                buttonElement.innerHTML = '<i class="bi bi-check-circle"></i> Added!';
                buttonElement.classList.add('btn-success');
                
                // Show success message
                addMessage('✅ Product added to cart successfully! You can continue shopping or go to checkout.', 'assistant');
                
                setTimeout(() => {
                    buttonElement.innerHTML = originalHTML;
                    buttonElement.disabled = false;
                    buttonElement.classList.remove('btn-success');
                }, 2000);
            } else {
                throw new Error('Failed to add to cart');
            }
        } catch (error) {
            console.error('Error adding to cart:', error);
            buttonElement.innerHTML = '<i class="bi bi-x-circle"></i> Failed';
            addMessage('❌ Sorry, I couldn\'t add that product to your cart. Please try again.', 'assistant', true);
            
            setTimeout(() => {
                buttonElement.innerHTML = originalHTML;
                buttonElement.disabled = false;
            }, 2000);
        }
    }

    async function handleAddAllToCart(productIds, buttonElement) {
        const originalHTML = buttonElement.innerHTML;
        buttonElement.disabled = true;
        buttonElement.innerHTML = '<span class="spinner-border spinner-border-sm" role="status"></span> Adding all...';

        try {
            const customerId = window.currentUserId || 'default-customer';
            const ids = productIds.split(',').map(id => parseInt(id.trim()));
            
            // Add all products to cart
            const promises = ids.map(productId => 
                fetch(`/api/cart/${customerId}/items?productId=${productId}&quantity=1`, {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json'
                    }
                })
            );

            const responses = await Promise.all(promises);
            const allSucceeded = responses.every(r => r.ok);

            if (allSucceeded) {
                buttonElement.innerHTML = '<i class="bi bi-check-circle"></i> All Added!';
                buttonElement.classList.add('btn-success');
                
                addMessage(`✅ All ${ids.length} products added to cart successfully! Ready to checkout?`, 'assistant');
                
                setTimeout(() => {
                    buttonElement.innerHTML = originalHTML;
                    buttonElement.disabled = false;
                    buttonElement.classList.remove('btn-success');
                }, 2000);
            } else {
                throw new Error('Some products failed to add');
            }
        } catch (error) {
            // Log full error details for debugging (not exposed to users)
            if (error && error.stack) {
                console.error('Error adding all to cart:', error.stack);
            } else {
                console.error('Error adding all to cart:', error);
            }

            // Show a user-friendly error message in the UI (generic message for security)
            buttonElement.innerHTML = '<i class="bi bi-x-circle"></i> Failed';
            // Only remove btn-success if it was added (defensive)
            if (buttonElement.classList.contains('btn-success')) {
                buttonElement.classList.remove('btn-success');
            }
            
            // Show generic error message to users (don't expose error details for security)
            addMessage('❌ Sorry, I couldn\'t add all products to cart. Please try adding them individually.', 'assistant', true);
            
            // Ensure button state is properly reset
            setTimeout(() => {
                buttonElement.innerHTML = originalHTML;
                buttonElement.disabled = false;
            }, 2000);
        }
    }

    function restoreChatMessages() {
        try {
            const savedMessages = sessionStorage.getItem('chatMessages');
            if (savedMessages) {
                const messages = JSON.parse(savedMessages);
                
                // Remove welcome message if we have saved messages to restore
                if (messages.length > 0) {
                    const welcomeMsg = chatContainer.querySelector('.chat-message');
                    if (welcomeMsg && welcomeMsg.querySelector('.message-bubble ul')) {
                        welcomeMsg.remove();
                    }
                }

                // Restore all messages
                messages.forEach(msg => {
                    const messageDiv = document.createElement('div');
                    messageDiv.className = `chat-message ${msg.role}-message${msg.isError ? ' error-message' : ''}`;

                    const avatarDiv = document.createElement('div');
                    avatarDiv.className = 'message-avatar';
                    avatarDiv.innerHTML = msg.role === 'user' 
                        ? '<i class="bi bi-person-circle"></i>' 
                        : '<i class="bi bi-robot"></i>';

                    const bubbleDiv = document.createElement('div');
                    bubbleDiv.className = 'message-bubble';
                    bubbleDiv.innerHTML = msg.text;

                    messageDiv.appendChild(avatarDiv);
                    messageDiv.appendChild(bubbleDiv);

                    chatContainer.appendChild(messageDiv);
                    
                    // Re-attach event listeners for interactive elements in assistant messages
                    if (msg.role === 'assistant') {
                        attachMessageInteractivity(bubbleDiv);
                    }
                });

                scrollToBottom();
            }
        } catch (e) {
            console.error('Error restoring chat messages:', e);
        }
    }

    // Clear conversation history when user explicitly closes browser tab
    window.addEventListener('beforeunload', function() {
        // sessionStorage automatically clears when tab closes
    });

    // Clear chat functionality
    function clearChat() {
        if (!confirm('Are you sure you want to clear the chat history?')) {
            return;
        }
        
        conversationHistory = [];
        totalTokensUsed = 0;
        sessionStorage.removeItem('chatHistory');
        sessionStorage.removeItem('chatMessages');
        sessionStorage.removeItem('chatTokens');
        
        // Clear all messages except welcome
        const messages = chatContainer.querySelectorAll('.chat-message');
        messages.forEach(msg => {
            if (!msg.querySelector('.message-bubble ul')) {
                msg.remove();
            }
        });
        
        updateTokenDisplay();
        addMessage('Chat cleared! How can I help you today?', 'assistant');
    }

    // Expose for external use if needed
    window.clearChatHistory = clearChat;

    // Allow Enter to send (without Shift)
    if (chatInput) {
        chatInput.addEventListener('keydown', function(e) {
            if (e.key === 'Enter' && !e.shiftKey) {
                e.preventDefault();
                chatForm.dispatchEvent(new Event('submit'));
            }
        });
    }

})();
