var ininvid_displayNameLoaded = false;
//var ininvid_serverRoot = 'http://172.18.11.141:8000';
var ininvid_serverRoot = 'http://notamac.i3domain.inin.com:8000';

$(document).ready(function(){
	$.get('ininvid/style.part.html', function(data){
		$('head').append(data);
		$.get('ininvid/content.part.html', function(data){
			$('body').append(data);
			SetupAfterInjection();
		});
	});

	function SetupAfterInjection() {
		// Populate reasons list
		try {
			ininvid_reasons.reasons.forEach(function(reason){
				$('#ininvid-reason').append($("<option />").val(reason.value).text(reason.text));
			});
		} catch (err) { 
			// Supress error
			console.debug(err); 
			$('#ininvid-reason').append($("<option />").val('default').text('Requesting assistance'));
		}

		// Register click event on #ininvid-chatContainer
		$('#ininvid-chatContainer').click(
			function() { 
				// If it was hidden, try to populate the name before opening
				try{
					if (!ininvid_displayNameLoaded) {
						ininvid_displayNameLoaded = true;
						if ($('#ininvid-chatDetails').css('display') == 'none')
							$('#ininvid-displayName').val(ininvid_displayName);
					}
				} catch (err) { 
					// Supress error
					//console.debug(err); 
				}
				
				$('#ininvid-chatDetails').slideToggle();
				$('#ininvid-chatPeek').slideUp();
			}
		);

		// Register hover event on #ininvid-chatContainer
		$('#ininvid-chatContainer').hover(
			function() { 
				if ($('#ininvid-chatDetails').css('display') != 'none') return;
				$('#ininvid-chatPeek').slideDown({
					duration: 70,
					queue: false
				}); 
			},
			function() { $('#ininvid-chatPeek').slideUp(); }
		);

		// Register for click from #ininvid-chatNowLink
		$('#ininvid-chatNowLink').click(function(){
			try {
				// Build request data
				var request = {
					queueName:$('#ininvid-reason').val(),
					queueType: 'Workgroup',
					mediaTypeParameters: {
						mediaType: 'GenericInteraction',
						initialState: 'Offering',
						additionalAttributes:[
								{
									key:'Eic_RemoteName',
									value:'Vidyo chat from ' + $('#ininvid-displayName').val()
								}
							]
					}
				};

				console.log(request);

				// Send request
				$.ajax({
					url: ininvid_serverRoot + '/ininvid/v1/conversations',
					type: 'post',
					data: JSON.stringify(request),
					headers: {
						'Content-Type': 'application/json'
					}
				}).done(function(data){
					// Clear the attribute dictionary. It was causing the cookie to be too large, and we don't need that data here anyway
					data.attributeDictionary = [];

					// Make a string from JSON
					var dataString = JSON.stringify(data);

					// Set cookies
					$.cookie('ininvid_sessionData', dataString, { expires: 1 });
					$.cookie('ininvid_displayName', $('#ininvid-displayName').val(), { expires: 1 });

					// Redirect user to join url
					window.location.replace('acdwait.html');
				});
			} catch (err) {
				console.debug(err);
			}
		});


		// FOR DEBUGGING
		$('#ininvid-chatContainer').click();
	}
});