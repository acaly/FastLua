local a = { val = 5 }
function a:x(i)
	return self.val * i
end
return a:x(10)
