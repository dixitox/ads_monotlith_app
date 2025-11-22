// AI Copilot Chat functionality
(function () {
    'use strict';

    const chatForm = document.getElementById('chatForm');
    const messageInput = document.getElementById('messageInput');
    const chatContainer = document.getElementById('chatContainer');
    const sendButton = document.getElementById('sendButton');

    // Store conversation history
    let conversationHistory = [];

    // Initialize
    if (chatForm) {
        chatForm.addEventListener('submit', handleSubmit);
        messageInput.focus();
    }

    async function handleSubmit(e) {
        e.preventDefault();

        const message = messageInput.value.trim();
        if (!message) return;

        // Disable input while processing
        setInputState(false);

        // Add user message to UI
        addMessage(message, 'user');

        // Clear input
        messageInput.value = '';

        // Add typing indicator
        const typingIndicator = addTypingIndicator();

        try {
            // Get CSRF token
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

        } catch (error) {
            console.error('Error sending message:', error);
            removeTypingIndicator(typingIndicator);
            addMessage('Sorry, I encountered an error. Please try again.', 'assistant', true);
        } finally {
            // Re-enable input
            setInputState(true);
            messageInput.focus();
        }
    }

    function addMessage(text, role, isError = false) {
        const messageDiv = document.createElement('div');
        messageDiv.className = `message ${role}-message${isError ? ' error-message' : ''}`;

        const avatarDiv = document.createElement('div');
        avatarDiv.className = 'message-avatar';
        avatarDiv.innerHTML = role === 'user' 
            ? '<i class="bi bi-person-circle"></i>' 
            : '<i class="bi bi-robot"></i>';

        const contentDiv = document.createElement('div');
        contentDiv.className = 'message-content';

        const textDiv = document.createElement('div');
        textDiv.className = 'message-text';
        
        // Convert markdown-style formatting to HTML
        textDiv.innerHTML = formatMessage(text);

        contentDiv.appendChild(textDiv);
        messageDiv.appendChild(avatarDiv);
        messageDiv.appendChild(contentDiv);

        chatContainer.appendChild(messageDiv);
        scrollToBottom();
    }

    function formatMessage(text) {
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

        return formatted;
    }

    function addTypingIndicator() {
        const messageDiv = document.createElement('div');
        messageDiv.className = 'message assistant-message typing-indicator';
        messageDiv.id = 'typingIndicator';

        const avatarDiv = document.createElement('div');
        avatarDiv.className = 'message-avatar';
        avatarDiv.innerHTML = '<i class="bi bi-robot"></i>';

        const contentDiv = document.createElement('div');
        contentDiv.className = 'message-content';

        const dotsDiv = document.createElement('div');
        dotsDiv.className = 'typing-dots';
        dotsDiv.innerHTML = '<span></span><span></span><span></span>';

        contentDiv.appendChild(dotsDiv);
        messageDiv.appendChild(avatarDiv);
        messageDiv.appendChild(contentDiv);

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
        messageInput.disabled = !enabled;
        sendButton.disabled = !enabled;
        
        if (enabled) {
            sendButton.innerHTML = '<i class="bi bi-send-fill"></i> Send';
        } else {
            sendButton.innerHTML = '<span class="spinner-border spinner-border-sm" role="status" aria-hidden="true"></span> Sending...';
        }
    }

    function scrollToBottom() {
        chatContainer.scrollTop = chatContainer.scrollHeight;
    }

    // Add keyboard shortcut info
    messageInput?.addEventListener('keydown', function(e) {
        // Allow Shift+Enter for new line if needed in future
        if (e.key === 'Enter' && !e.shiftKey) {
            e.preventDefault();
            chatForm.dispatchEvent(new Event('submit'));
        }
    });

})();
